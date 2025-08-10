using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Service implementation for core chat operations using OpenAI models.
/// Handles chat completions, message exchange, and semaphore-based concurrency control for chat instances.
/// </summary>
public class ChatCoreService : IChatCoreService
{
    private readonly ILogger<ChatCoreService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly ChatClient _openAIClient;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private const float TEMPERATURE = 0.6f;

    /// <summary>
    /// Initializes a new instance of the ChatCoreService.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="messageCacheService">Service for caching and managing messages.</param>
    /// <param name="openAIClient">OpenAI chat client for AI model interactions.</param>
    /// <param name="chatSemaphoreManager">Manager for handling chat operation synchronization.</param>
    public ChatCoreService(ILogger<ChatCoreService> logger, IMessageCacheService messageCacheService, ChatClient openAIClient, IChatSemaphoreManager chatSemaphoreManager)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _openAIClient = openAIClient;
        _chatSemaphoreManager = chatSemaphoreManager;
    }

    /// <inheritdoc/>
    public async Task<ChatCompletion> ChatAsync(ulong instanceId, List<ChatMessage> messageHistory, ChatMessage? personaMessage = null)
    {
        // Use a semaphore to prevent concurrent updates for the same channel.
        var instanceSemaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(instanceId);
        await instanceSemaphore.WaitAsync();
        try
        {
            if (personaMessage is null)
            {
                personaMessage = await GetCorePersonaAsync();
            }

            // Create a new list for LLM request with system message at the beginning
            // Don't modify the original messageHistory to avoid affecting cached conversation
            var messagesForLLM = new List<ChatMessage> { personaMessage };
            messagesForLLM.AddRange(messageHistory);

            // Set up chat options.
            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1200,
                Temperature = TEMPERATURE,
            };

            // Call the chat API.
            return await _openAIClient.CompleteChatAsync(messagesForLLM, options);
        }
        finally
        {
            instanceSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExchangeMessageAsync(string message, ChatMessage? developerPersonaChatMessage = null, int tokenLimit = 1200, ulong? instanceId = null)
    {
        // Use the provided instance ID or a generic semaphore for non-instance-specific exchanges
        // We use 0 as the instance ID for general message exchanges that don't belong to a specific chat session
        var effectiveInstanceId = instanceId ?? 0;
        var semaphore = _chatSemaphoreManager.GetOrCreateInstanceSemaphore(effectiveInstanceId);
        await semaphore.WaitAsync();
        
        try
        {
            if (developerPersonaChatMessage is null)
            {
                developerPersonaChatMessage = await GetCorePersonaAsync();
            }

            var userMessage = ChatMessage.CreateUserMessage(message);

            var messages = new List<ChatMessage> { developerPersonaChatMessage, userMessage };
            ChatCompletionOptions options = new()
            {
                MaxOutputTokenCount = tokenLimit,
                Temperature = 0.6f,
            };

            var response = await _openAIClient.CompleteChatAsync(messages, options);

            return response.Value.Content.First().Text;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<ChatMessage> GetCorePersonaAsync()
    {
        var personaMessage = await _messageCacheService.GetPersonaCoreMessageAsync();
        return ChatMessage.CreateSystemMessage(personaMessage);
    }
}