using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Multi-provider implementation of chat service that supports OpenAI, Gemini, Grok, etc.
/// </summary>
public class MultiProviderChatService : IChatCoreService
{
    private readonly ILogger<MultiProviderChatService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatProviderFactory _providerFactory;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly ChatOptions _chatOptions;
    
    public MultiProviderChatService(
        ILogger<MultiProviderChatService> logger,
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
    
    /// <inheritdoc/>
    public async Task<OpenAI.Chat.ChatCompletion> ChatAsync(
        ulong instanceId, 
        List<OpenAI.Chat.ChatMessage> messageHistory, 
        OpenAI.Chat.ChatMessage? personaMessage = null)
    {
        // Use a semaphore to prevent concurrent updates for the same channel
        var instanceSemaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(instanceId);
        await instanceSemaphore.WaitAsync();
        
        try
        {
            // Get persona message if not provided
            if (personaMessage is null)
            {
                personaMessage = await GetCorePersonaAsync();
            }
            
            messageHistory.Insert(0, personaMessage);
            
            // Convert OpenAI messages to our format
            var sessionMessages = ConvertToSessionMessages(messageHistory);
            
            // Get the appropriate provider
            var provider = GetProviderWithFallback();
            
            // Set up chat options
            var options = new ChatCompletionOptions
            {
                MaxTokens = _chatOptions.TokenLimit,
                Temperature = _chatOptions.Temperature,
                Model = _chatOptions.Model
            };
            
            // Call the provider
            var response = await CallProviderWithFallback(provider, sessionMessages, options);
            
            // Convert response back to OpenAI format for compatibility
            return ConvertToOpenAICompletion(response);
        }
        finally
        {
            instanceSemaphore.Release();
        }
    }
    
    /// <inheritdoc/>
    public async Task<string> ExchangeMessageAsync(
        string message, 
        OpenAI.Chat.ChatMessage? developerPersonaChatMessage = null, 
        int tokenLimit = 1200)
    {
        if (developerPersonaChatMessage is null)
        {
            developerPersonaChatMessage = await GetCorePersonaAsync();
        }
        
        var userMessage = OpenAI.Chat.ChatMessage.CreateUserMessage(message);
        var messages = new List<OpenAI.Chat.ChatMessage> { developerPersonaChatMessage, userMessage };
        
        // Convert to session messages
        var sessionMessages = ConvertToSessionMessages(messages);
        
        // Get provider
        var provider = GetProviderWithFallback();
        
        // Set up options
        var options = new ChatCompletionOptions
        {
            MaxTokens = tokenLimit,
            Temperature = _chatOptions.Temperature
        };
        
        // Call provider
        var response = await CallProviderWithFallback(provider, sessionMessages, options);
        
        return response.Content;
    }
    
    private IChatProvider GetProviderWithFallback()
    {
        try
        {
            return _providerFactory.GetProvider(_chatOptions.Provider);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get provider {Provider}, using default", _chatOptions.Provider);
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
        catch (Exception ex) when (_chatOptions.EnableFallback)
        {
            _logger.LogError(ex, "Provider {Provider} failed, attempting fallback", provider.ProviderName);
            
            // Try fallback providers
            foreach (var fallbackProviderName in _chatOptions.FallbackProviders)
            {
                if (fallbackProviderName == provider.ProviderName)
                    continue;
                
                try
                {
                    var fallbackProvider = _providerFactory.GetProvider(fallbackProviderName);
                    if (await fallbackProvider.IsAvailableAsync())
                    {
                        _logger.LogInformation("Using fallback provider {Provider}", fallbackProviderName);
                        return await fallbackProvider.ChatAsync(messages, options);
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Fallback provider {Provider} failed", fallbackProviderName);
                }
            }
            
            throw new InvalidOperationException("All providers failed", ex);
        }
    }
    
    private List<SessionMessage> ConvertToSessionMessages(List<OpenAI.Chat.ChatMessage> messages)
    {
        return messages.Select(m => new SessionMessage
        {
            Id = Guid.NewGuid().ToString(),
            Role = GetRoleString(m),
            Content = m.Content?.FirstOrDefault()?.Text ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            IncludeInContext = true
        }).ToList();
    }
    
    private OpenAI.Chat.ChatCompletion ConvertToOpenAICompletion(ChatCompletionResponse response)
    {
        // This is a wrapper to maintain compatibility with existing code
        // In a real implementation, we'd need to create a proper wrapper or refactor the interface
        var completion = new OpenAICompletionWrapper
        {
            Content = response.Content,
            Model = response.Model ?? _chatOptions.Model,
            Role = response.Role,
            Usage = new OpenAIUsageWrapper
            {
                InputTokenCount = response.PromptTokens ?? 0,
                OutputTokenCount = response.CompletionTokens ?? 0,
                TotalTokenCount = response.TotalTokens ?? 0
            }
        };
        
        // Note: This requires creating wrapper classes or refactoring the interface
        // For now, we'll need to update the IChatCoreService interface
        throw new NotImplementedException("Conversion to OpenAI.Chat.ChatCompletion requires wrapper implementation");
    }
    
    private async Task<OpenAI.Chat.ChatMessage> GetCorePersonaAsync()
    {
        var personaMessage = await _messageCacheService.GetPersonaCoreMessageAsync();
        return OpenAI.Chat.ChatMessage.CreateSystemMessage(personaMessage);
    }
    
    private string GetRoleString(OpenAI.Chat.ChatMessage message)
    {
        // Since we can't access the Role property directly, we need to check the message content
        var content = message.Content?.FirstOrDefault();
        if (content != null)
        {
            // This is a workaround - in a real implementation we'd need better access to the role
            return "user"; // Default to user for now
        }
        return "user";
    }
    
    // Temporary wrapper classes (should be properly implemented)
    private class OpenAICompletionWrapper
    {
        public string Content { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public OpenAIUsageWrapper? Usage { get; set; }
    }
    
    private class OpenAIUsageWrapper
    {
        public int InputTokenCount { get; set; }
        public int OutputTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }
}