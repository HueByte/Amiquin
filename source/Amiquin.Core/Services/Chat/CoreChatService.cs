using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Core chat service that handles provider selection and fallback logic
/// </summary>
public class CoreChatService : IChatCoreService
{
    private readonly ILogger<CoreChatService> _logger;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IMessageCacheService _messageCache;
    private readonly ISemaphoreManager _semaphoreManager;
    private readonly LLMOptions _llmOptions;

    public CoreChatService(
        ILogger<CoreChatService> logger,
        IChatProviderFactory providerFactory,
        IMessageCacheService messageCache,
        ISemaphoreManager semaphoreManager,
        IOptions<LLMOptions> llmOptions)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _messageCache = messageCache;
        _semaphoreManager = semaphoreManager;
        _llmOptions = llmOptions.Value;
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> CoreRequestAsync(
        string prompt,
        string? customPersona = null,
        int tokenLimit = 1200,
        string? provider = null)
    {
        // Build system message
        var baseSystem = await _messageCache.GetSystemCoreMessageAsync() ?? _llmOptions.GlobalSystemMessage;
        var systemMessage = BuildSystemMessage(baseSystem, customPersona);

        // Create messages for the request
        var messages = new List<SessionMessage>
        {
            new() { Role = "system", Content = systemMessage, CreatedAt = DateTime.UtcNow },
            new() { Role = "user", Content = prompt, CreatedAt = DateTime.UtcNow }
        };

        // Execute with provider selection and fallback
        return await ExecuteWithFallbackAsync(messages, tokenLimit, provider);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? provider = null)
    {
        return await ChatAsync(instanceId, messages, customPersona, null, provider);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? provider = null)
    {
        return await ChatAsync(instanceId, messages, customPersona, sessionContext, sessionId: null, provider);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? sessionId = null,
        string? provider = null)
    {
        return await ChatAsync(instanceId, messages, customPersona, sessionContext, sessionId, provider, model: null);
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? sessionId = null,
        string? provider = null,
        string? model = null)
    {
        // Use semaphore to prevent concurrent requests for the same instance
        var semaphore = _semaphoreManager.GetOrCreateInstanceSemaphore(instanceId.ToString());
        await semaphore.WaitAsync();

        try
        {
            // Build system message with all components
            var baseSystem = await _messageCache.GetSystemCoreMessageAsync() ?? _llmOptions.GlobalSystemMessage;
            var systemMessage = BuildSystemMessage(baseSystem, customPersona, sessionContext);

            // Prepare messages with system message first
            var fullMessages = new List<SessionMessage>
            {
                new() { Role = "system", Content = systemMessage, CreatedAt = DateTime.UtcNow }
            };
            fullMessages.AddRange(messages);

            // Execute with provider selection and fallback, passing session ID and model for cache optimization
            return await ExecuteWithFallbackAsync(fullMessages, null, provider, sessionId, model);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ChatCompletionResponse> ChatWithMemoryContextAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? memoryContext = null,
        string? sessionId = null,
        string? provider = null,
        string? model = null)
    {
        // Use semaphore to prevent concurrent requests for the same instance
        var semaphore = _semaphoreManager.GetOrCreateInstanceSemaphore(instanceId.ToString());
        await semaphore.WaitAsync();

        try
        {
            // Build system message with STABLE components only (for cache optimization)
            // Memory context is NOT included here - it changes frequently and would invalidate cache
            var baseSystem = await _messageCache.GetSystemCoreMessageAsync() ?? _llmOptions.GlobalSystemMessage;
            var systemMessage = BuildSystemMessage(baseSystem, customPersona, sessionContext);

            // Prepare messages with cache-optimized ordering:
            // 1. System message (stable - cached)
            // 2. Conversation history (semi-stable - partially cached)
            // 3. Memory context as system note (dynamic - not cached, appended)
            var fullMessages = new List<SessionMessage>
            {
                new() { Role = "system", Content = systemMessage, CreatedAt = DateTime.UtcNow }
            };
            fullMessages.AddRange(messages);

            // Append memory context AFTER conversation history as a system note
            // This ensures the cache prefix (system + history) remains stable
            if (!string.IsNullOrWhiteSpace(memoryContext))
            {
                fullMessages.Add(new SessionMessage
                {
                    Role = "system",
                    Content = $"[Memory Context - use this information to inform your response]\n{memoryContext}",
                    CreatedAt = DateTime.UtcNow,
                    IncludeInContext = false // Don't persist this injected context
                });
            }

            // Execute with provider selection and fallback
            return await ExecuteWithFallbackAsync(fullMessages, null, provider, sessionId, model);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private string BuildSystemMessage(string basePersona, string? customPersona = null, string? sessionContext = null)
    {
        var parts = new List<string> { basePersona };

        if (!string.IsNullOrWhiteSpace(customPersona))
        {
            parts.Add(customPersona);
        }

        if (!string.IsNullOrWhiteSpace(sessionContext))
        {
            parts.Add($"Previous conversation context:\n{sessionContext}");
        }

        return string.Join("\n\n", parts);
    }

    private async Task<ChatCompletionResponse> ExecuteWithFallbackAsync(
        List<SessionMessage> messages,
        int? maxTokens,
        string? preferredProvider,
        string? sessionId = null,
        string? model = null)
    {
        var options = new ChatCompletionOptions
        {
            MaxTokens = maxTokens ?? 1200,
            Temperature = _llmOptions.GlobalTemperature,
            // Pass session ID for prompt cache optimization (Grok uses x-grok-conv-id header)
            ConversationId = sessionId,
            // Pass specific model if provided (overrides provider's default model)
            Model = model
        };

        if (!string.IsNullOrWhiteSpace(model))
        {
            _logger.LogInformation("Using specific model: {Model}", model);
        }

        // Determine provider order
        var providersToTry = GetProviderOrder(preferredProvider);

        foreach (var providerName in providersToTry)
        {
            try
            {
                var provider = _providerFactory.GetProvider(providerName);
                if (provider == null)
                {
                    _logger.LogWarning("Provider {Provider} not found", providerName);
                    continue;
                }

                if (!await provider.IsAvailableAsync())
                {
                    _logger.LogWarning("Provider {Provider} is not available", providerName);
                    continue;
                }

                _logger.LogInformation("Attempting to use provider: {Provider}", providerName);
                var response = await provider.ChatAsync(messages, options);

                _logger.LogInformation("Successfully received response from {Provider}", providerName);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get response from provider {Provider}", providerName);

                if (!_llmOptions.EnableFallback || providerName == providersToTry.Last())
                {
                    throw new Exception($"All providers failed. Last error from {providerName}: {ex.Message}", ex);
                }
            }
        }

        throw new Exception("No providers available to handle the request");
    }

    private List<string> GetProviderOrder(string? preferredProvider)
    {
        var order = new List<string>();

        // Add preferred provider first if specified
        if (!string.IsNullOrWhiteSpace(preferredProvider))
        {
            order.Add(preferredProvider);
        }

        // Add fallback order, excluding any already added
        foreach (var provider in _llmOptions.FallbackOrder)
        {
            if (!order.Contains(provider, StringComparer.OrdinalIgnoreCase))
            {
                order.Add(provider);
            }
        }

        // If nothing specified, use default provider
        if (order.Count == 0 && !string.IsNullOrWhiteSpace(_llmOptions.DefaultProvider))
        {
            order.Add(_llmOptions.DefaultProvider);
        }

        return order;
    }
}