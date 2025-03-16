using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly IMessageCacheService _messageCacheService;
    private readonly ChatClient _openAIClient;
    private const int MAX_TOKENS_TOTAL = 10_000; // to configuration later

    public ChatService(ILogger<ChatService> logger, IMessageCacheService messageCacheService, ChatClient openAIClient)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _openAIClient = openAIClient;
    }

    public async Task<string> ChatAsync(ulong channelId, ulong userId, string message)
    {
        List<OpenAI.Chat.ChatMessage> messages = await _messageCacheService.GetChatMessages(channelId);
        var userChatMessage = ChatMessage.CreateUserMessage(message);
        messages.Add(userChatMessage);

        ChatCompletionOptions options = new()
        {
            MaxOutputTokenCount = 1200,
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

        var assistantChatMessage = ChatMessage.CreateAssistantMessage(responseMessage);
        await _messageCacheService.AddOrUpdateChatMessage(channelId, assistantChatMessage);

        return responseMessage.First().Text;
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