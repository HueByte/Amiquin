using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Implementation of multi-provider chat service supporting OpenAI, Gemini, Grok, etc.
/// </summary>
public class MultiProviderChatServiceImpl : IMultiProviderChatService
{
    private readonly ILogger<MultiProviderChatServiceImpl> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly ChatOptions _chatOptions;
    
    public MultiProviderChatServiceImpl(
        ILogger<MultiProviderChatServiceImpl> logger,
        IMessageCacheService messageCacheService,
        IChatProviderFactory providerFactory,
        IChatSemaphoreManager chatSemaphoreManager,
        IOptions<ChatOptions> chatOptions)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _providerFactory = providerFactory;
        _chatSemaphoreManager = chatSemaphoreManager;
        _chatOptions = chatOptions.Value;
    }
    
    public async Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId, 
        List<SessionMessage> messages, 
        string? systemMessage = null,
        string? provider = null)
    {
        // Use a semaphore to prevent concurrent updates for the same instance
        var instanceSemaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(instanceId);
        await instanceSemaphore.WaitAsync();
        
        try
        {
            // Get system message if not provided
            if (string.IsNullOrEmpty(systemMessage))
            {
                systemMessage = await _messageCacheService.GetPersonaCoreMessageAsync();
            }
            
            // Ensure system message is at the beginning
            var processedMessages = PrepareMessages(messages, systemMessage!);
            
            // Get the appropriate provider
            var chatProvider = GetProvider(provider);
            
            // Set up chat options
            var options = new ChatCompletionOptions
            {
                MaxTokens = _chatOptions.TokenLimit,
                Temperature = _chatOptions.Temperature,
                Model = provider == null ? _chatOptions.Model : null // Use configured model only if no provider override
            };
            
            // Call the provider with fallback support
            var response = await CallProviderWithFallback(chatProvider, processedMessages, options);
            
            _logger.LogInformation(
                "Chat completion successful for instance {InstanceId} using provider {Provider}",
                instanceId, 
                response.Metadata?.GetValueOrDefault("provider") ?? chatProvider.ProviderName);
            
            return response;
        }
        finally
        {
            instanceSemaphore.Release();
        }
    }
    
    public async Task<ChatCompletionResponse> ExchangeMessageAsync(
        string message, 
        string? systemMessage = null, 
        int tokenLimit = 1200,
        string? provider = null)
    {
        // Get system message if not provided
        if (string.IsNullOrEmpty(systemMessage))
        {
            systemMessage = await _messageCacheService.GetPersonaCoreMessageAsync();
        }
        
        // Create message list
        var messages = new List<SessionMessage>
        {
            new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "system",
                Content = systemMessage!,
                CreatedAt = DateTime.UtcNow
            },
            new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = message,
                CreatedAt = DateTime.UtcNow
            }
        };
        
        // Get provider
        var chatProvider = GetProvider(provider);
        
        // Set up options
        var options = new ChatCompletionOptions
        {
            MaxTokens = tokenLimit,
            Temperature = _chatOptions.Temperature
        };
        
        // Call provider
        var response = await CallProviderWithFallback(chatProvider, messages, options);
        
        _logger.LogDebug("Message exchange completed using provider {Provider}", 
            response.Metadata?.GetValueOrDefault("provider") ?? chatProvider.ProviderName);
        
        return response;
    }
    
    public string GetCurrentProvider()
    {
        return _chatOptions.Provider;
    }
    
    public IEnumerable<string> GetAvailableProviders()
    {
        return _providerFactory.GetAvailableProviders();
    }
    
    public async Task<bool> IsProviderAvailableAsync(string providerName)
    {
        try
        {
            var provider = _providerFactory.GetProvider(providerName);
            return await provider.IsAvailableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check availability for provider {Provider}", providerName);
            return false;
        }
    }
    
    private IChatProvider GetProvider(string? providerName)
    {
        try
        {
            if (!string.IsNullOrEmpty(providerName))
            {
                _logger.LogDebug("Using specified provider: {Provider}", providerName);
                return _providerFactory.GetProvider(providerName);
            }
            
            _logger.LogDebug("Using configured provider: {Provider}", _chatOptions.Provider);
            return _providerFactory.GetProvider(_chatOptions.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get provider {Provider}, using default", 
                providerName ?? _chatOptions.Provider);
            return _providerFactory.GetDefaultProvider();
        }
    }
    
    private async Task<ChatCompletionResponse> CallProviderWithFallback(
        IChatProvider provider,
        List<SessionMessage> messages,
        ChatCompletionOptions options)
    {
        try
        {
            _logger.LogDebug("Calling {Provider} for chat completion", provider.ProviderName);
            return await provider.ChatAsync(messages, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider {Provider} failed", provider.ProviderName);
            
            if (!_chatOptions.EnableFallback)
            {
                throw;
            }
            
            // Try fallback providers
            foreach (var fallbackProviderName in _chatOptions.FallbackProviders)
            {
                if (fallbackProviderName == provider.ProviderName)
                    continue;
                
                try
                {
                    var fallbackProvider = _providerFactory.GetProvider(fallbackProviderName);
                    
                    // Check if provider is available
                    if (!await fallbackProvider.IsAvailableAsync())
                    {
                        _logger.LogWarning("Fallback provider {Provider} is not available", fallbackProviderName);
                        continue;
                    }
                    
                    _logger.LogInformation("Using fallback provider {Provider}", fallbackProviderName);
                    return await fallbackProvider.ChatAsync(messages, options);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Fallback provider {Provider} failed", fallbackProviderName);
                }
            }
            
            throw new InvalidOperationException($"All providers failed. Original error from {provider.ProviderName}", ex);
        }
    }
    
    private List<SessionMessage> PrepareMessages(List<SessionMessage> messages, string systemMessage)
    {
        var result = new List<SessionMessage>();
        
        // Add system message first if not already present
        if (!messages.Any(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "system",
                Content = systemMessage,
                CreatedAt = DateTime.UtcNow,
                IncludeInContext = true
            });
        }
        
        // Add all other messages
        result.AddRange(messages);
        
        return result;
    }
}