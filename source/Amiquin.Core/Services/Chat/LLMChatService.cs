using Amiquin.Core.Models;
using Amiquin.Core.Options.Configuration;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// LLM-based chat service that uses the new LLM configuration system
/// </summary>
public class LLMChatService : IMultiProviderChatService
{
    private readonly ILogger<LLMChatService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly LLMOptions _llmOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, IChatProvider> _providers;
    
    public LLMChatService(
        ILogger<LLMChatService> logger,
        IMessageCacheService messageCacheService,
        IChatSemaphoreManager chatSemaphoreManager,
        IOptions<LLMOptions> llmOptions,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _chatSemaphoreManager = chatSemaphoreManager;
        _llmOptions = llmOptions.Value;
        _httpClientFactory = httpClientFactory;
        _providers = new Dictionary<string, IChatProvider>();
        
        InitializeProviders();
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
                systemMessage = await _messageCacheService.GetPersonaCoreMessageAsync() 
                    ?? _llmOptions.GlobalSystemMessage;
            }
            
            // Prepare messages with system message
            var processedMessages = PrepareMessages(messages, systemMessage);
            
            // Get the appropriate provider
            var chatProvider = GetProvider(provider ?? _llmOptions.DefaultProvider);
            var providerConfig = _llmOptions.GetProvider(chatProvider.ProviderName);
            var modelConfig = providerConfig?.GetDefaultModel();
            
            // Set up chat options using model configuration
            var options = new ChatCompletionOptions
            {
                MaxTokens = modelConfig?.MaxOutputTokens ?? 2048,
                Temperature = modelConfig?.GetEffectiveTemperature(_llmOptions.GlobalTemperature) ?? _llmOptions.GlobalTemperature,
                Model = providerConfig?.DefaultModel
            };
            
            // Call the provider with fallback support
            var response = await CallProviderWithFallback(chatProvider, processedMessages, options);
            
            _logger.LogInformation(
                "Chat completion successful for instance {InstanceId} using provider {Provider} model {Model}",
                instanceId, 
                response.Metadata?.GetValueOrDefault("provider") ?? chatProvider.ProviderName,
                response.Model ?? options.Model);
            
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
            systemMessage = await _messageCacheService.GetPersonaCoreMessageAsync()
                ?? _llmOptions.GlobalSystemMessage;
        }
        
        // Create message list
        var messages = new List<SessionMessage>
        {
            new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "system",
                Content = systemMessage,
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
        
        // Get provider and configuration
        var chatProvider = GetProvider(provider ?? _llmOptions.DefaultProvider);
        var providerConfig = _llmOptions.GetProvider(chatProvider.ProviderName);
        var modelConfig = providerConfig?.GetDefaultModel();
        
        // Set up options
        var options = new ChatCompletionOptions
        {
            MaxTokens = Math.Min(tokenLimit, modelConfig?.MaxOutputTokens ?? tokenLimit),
            Temperature = modelConfig?.GetEffectiveTemperature(_llmOptions.GlobalTemperature) ?? _llmOptions.GlobalTemperature,
            Model = providerConfig?.DefaultModel
        };
        
        // Call provider
        var response = await CallProviderWithFallback(chatProvider, messages, options);
        
        _logger.LogDebug("Message exchange completed using provider {Provider} model {Model}", 
            response.Metadata?.GetValueOrDefault("provider") ?? chatProvider.ProviderName,
            response.Model ?? options.Model);
        
        return response;
    }
    
    public string GetCurrentProvider()
    {
        return _llmOptions.DefaultProvider;
    }
    
    public IEnumerable<string> GetAvailableProviders()
    {
        return _llmOptions.Providers.Where(p => p.Value.Enabled).Select(p => p.Key);
    }
    
    public async Task<bool> IsProviderAvailableAsync(string providerName)
    {
        try
        {
            if (!_providers.TryGetValue(providerName, out var provider))
            {
                return false;
            }
            
            var providerConfig = _llmOptions.GetProvider(providerName);
            if (providerConfig == null || !providerConfig.Enabled)
            {
                return false;
            }
            
            return await provider.IsAvailableAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check availability for provider {Provider}", providerName);
            return false;
        }
    }
    
    /// <summary>
    /// Gets available models for a specific provider
    /// </summary>
    public IEnumerable<string> GetAvailableModels(string providerName)
    {
        var providerConfig = _llmOptions.GetProvider(providerName);
        return providerConfig?.Models.Keys ?? Enumerable.Empty<string>();
    }
    
    /// <summary>
    /// Gets model information for a specific provider and model
    /// </summary>
    public LLMModelOptions? GetModelInfo(string providerName, string modelName)
    {
        return _llmOptions.GetProvider(providerName)?.GetModel(modelName);
    }
    
    private void InitializeProviders()
    {
        foreach (var providerConfig in _llmOptions.Providers.Where(p => p.Value.Enabled))
        {
            try
            {
                var provider = CreateProvider(providerConfig.Key, providerConfig.Value);
                if (provider != null)
                {
                    _providers[providerConfig.Key] = provider;
                    _logger.LogInformation("Initialized provider: {Provider}", providerConfig.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize provider: {Provider}", providerConfig.Key);
            }
        }
    }
    
    private IChatProvider? CreateProvider(string providerName, LLMProviderOptions config)
    {
        return providerName.ToUpperInvariant() switch
        {
            "OPENAI" => new OpenAILLMProvider(_logger, _httpClientFactory, config, _llmOptions),
            "GROK" => new GrokLLMProvider(_logger, _httpClientFactory, config, _llmOptions),
            "GEMINI" => new GeminiLLMProvider(_logger, _httpClientFactory, config, _llmOptions),
            _ => null
        };
    }
    
    private IChatProvider GetProvider(string providerName)
    {
        if (_providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }
        
        // Try to find a case-insensitive match
        var match = _providers.FirstOrDefault(p => 
            string.Equals(p.Key, providerName, StringComparison.OrdinalIgnoreCase));
        
        if (match.Value != null)
        {
            return match.Value;
        }
        
        _logger.LogWarning("Provider {Provider} not found, using default provider {DefaultProvider}", 
            providerName, _llmOptions.DefaultProvider);
        
        if (_providers.TryGetValue(_llmOptions.DefaultProvider, out var defaultProvider))
        {
            return defaultProvider;
        }
        
        // If default provider is not available, use the first available provider
        var firstProvider = _providers.Values.FirstOrDefault();
        if (firstProvider != null)
        {
            _logger.LogWarning("Default provider not available, using {Provider}", firstProvider.ProviderName);
            return firstProvider;
        }
        
        throw new InvalidOperationException("No chat providers are available");
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
            
            if (!_llmOptions.EnableFallback)
            {
                throw;
            }
            
            // Try fallback providers
            foreach (var fallbackProviderName in _llmOptions.FallbackOrder)
            {
                if (fallbackProviderName == provider.ProviderName)
                    continue;
                
                try
                {
                    if (!_providers.TryGetValue(fallbackProviderName, out var fallbackProvider))
                        continue;
                    
                    // Check if provider is available
                    if (!await fallbackProvider.IsAvailableAsync())
                    {
                        _logger.LogWarning("Fallback provider {Provider} is not available", fallbackProviderName);
                        continue;
                    }
                    
                    _logger.LogInformation("Using fallback provider {Provider}", fallbackProviderName);
                    
                    // Update options for the fallback provider
                    var fallbackConfig = _llmOptions.GetProvider(fallbackProviderName);
                    var fallbackModelConfig = fallbackConfig?.GetDefaultModel();
                    
                    var fallbackOptions = new ChatCompletionOptions
                    {
                        MaxTokens = Math.Min(options.MaxTokens, fallbackModelConfig?.MaxOutputTokens ?? options.MaxTokens),
                        Temperature = fallbackModelConfig?.GetEffectiveTemperature(options.Temperature) ?? options.Temperature,
                        Model = fallbackConfig?.DefaultModel,
                        TopP = options.TopP,
                        FrequencyPenalty = options.FrequencyPenalty,
                        PresencePenalty = options.PresencePenalty,
                        StopSequences = options.StopSequences,
                        User = options.User
                    };
                    
                    return await fallbackProvider.ChatAsync(messages, fallbackOptions);
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