using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public class ChatCoreService : IChatCoreService
{
    private readonly ILogger<ChatCoreService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly ChatClient _openAIClient;
    private const int MAX_TOKENS_TOTAL = 10_000; // to configuration later
    private readonly IChatSemaphoreManager _chatSemaphoreManager;

    public ChatCoreService(ILogger<ChatCoreService> logger, IMessageCacheService messageCacheService, ChatClient openAIClient, IChatSemaphoreManager chatSemaphoreManager)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _openAIClient = openAIClient;
        _chatSemaphoreManager = chatSemaphoreManager;
    }

    public async Task<string> ChatAsync(ulong channelId, ulong userId, string message, ChatMessage? personaChatMessage = null)
    {
        var channelSemaphore = _chatSemaphoreManager.GetOrCreateSemaphore(channelId);
        await channelSemaphore.WaitAsync();

        string result = string.Empty;
        try
        {
            List<OpenAI.Chat.ChatMessage> messages = _messageCacheService.GetChatMessages(channelId)!;
            if (messages is null || messages.Count == 0)
            {
                // Developer message
                messages ??= new List<ChatMessage>();
                if (personaChatMessage is null)
                {
                    string? personaMessage = await _messageCacheService.GetPersonaCoreMessage();
                    personaChatMessage = ChatMessage.CreateDeveloperMessage(personaMessage);
                }

                messages.Add(personaChatMessage);
            }

            // User message
            var userChatMessage = ChatMessage.CreateUserMessage(message);
            messages.Add(userChatMessage);

            ChatCompletionOptions options = new()
            {
                MaxOutputTokenCount = 1200,
                Temperature = 0.6f,
            };

            var response = await _openAIClient.CompleteChatAsync(messages, options);
            var responseMessage = response.Value.Content;

            var usage = response.Value.Usage;
            _logger.LogInformation("Chat used: {totalTokens} Total ~ {input} Input ~ {output} Output", usage.TotalTokenCount, usage.InputTokenCount, usage.OutputTokenCount);

            if (usage.TotalTokenCount > MAX_TOKENS_TOTAL)
            {
                _logger.LogWarning("Chat used {totalTokens} tokens while the limit is {limit}, performing optimization", usage.TotalTokenCount, MAX_TOKENS_TOTAL);
                var messagesToRemoveCount = await OptimizeMessagesAsync(usage.TotalTokenCount, messages);
                _messageCacheService.ClearOldMessages(channelId, messagesToRemoveCount);
            }

            // AI message
            var assistantChatMessage = ChatMessage.CreateAssistantMessage(responseMessage);
            messages.Add(assistantChatMessage);
            _messageCacheService.SetChatMessages(channelId, messages);
            result = responseMessage.First().Text;
        }
        finally
        {
            channelSemaphore.Release();
        }

        return result;
    }

    public async Task<string> ExchangeMessageAsync(string message, ChatMessage? developerPersonaChatMessage = null, int tokenLimit = 1200)
    {
        if (developerPersonaChatMessage is null)
        {
            string? personaMessage = await _messageCacheService.GetPersonaCoreMessage();
            developerPersonaChatMessage = ChatMessage.CreateDeveloperMessage(personaMessage);
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

    private async Task<int> OptimizeMessagesAsync(int currentTokenCount, List<ChatMessage> messages)
    {
        int targetTokenCount = MAX_TOKENS_TOTAL / 2;
        int messagesToRemove = 0;

        foreach (var message in messages)
        {
            var text = message.Content.First().Text;
            int tokenCount = await Tokenizer.CountTokensAsync(text);

            if (currentTokenCount - tokenCount < targetTokenCount)
            {
                break;
            }

            currentTokenCount -= tokenCount;
            messagesToRemove++;
        }

        _logger.LogInformation("Removed {messagesToRemove} messages | Expected current token count: {tokenCount}", messagesToRemove, currentTokenCount);
        return messagesToRemove;
    }
}