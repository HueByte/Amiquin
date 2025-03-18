using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
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
    private readonly IMessageRepository _messageRepository;
    public ChatCoreService(ILogger<ChatCoreService> logger, IMessageCacheService messageCacheService, ChatClient openAIClient, IChatSemaphoreManager chatSemaphoreManager, IMessageRepository messageRepository)
    {
        _logger = logger;
        _messageCacheService = messageCacheService;
        _openAIClient = openAIClient;
        _chatSemaphoreManager = chatSemaphoreManager;
        _messageRepository = messageRepository;
    }

    public async Task<string> ChatAsync(ulong channelId, ulong userId, ulong botId, string message, ChatMessage? personaChatMessage = null)
    {
        // Use a semaphore to prevent concurrent updates for the same channel.
        var channelSemaphore = _chatSemaphoreManager.GetOrCreateTextSemaphore(channelId);
        await channelSemaphore.WaitAsync();

        try
        {
            // Retrieve the conversation history from cache (clone if needed).
            var cachedMessages = await _messageCacheService.GetOrCreateChatMessagesAsync(channelId)
                                 ?? new List<OpenAI.Chat.ChatMessage>();

            var conversationHistory = new List<OpenAI.Chat.ChatMessage>(cachedMessages);

            // Ensure a valid persona message.
            if (personaChatMessage is null)
            {
                string? personaCoreMessage = await _messageCacheService.GetPersonaCoreMessage();
                personaChatMessage = ChatMessage.CreateDeveloperMessage(personaCoreMessage);
            }

            // Build the conversation for the API call without mutating the saved history.
            var conversationForChat = new List<OpenAI.Chat.ChatMessage> { personaChatMessage };
            conversationForChat.AddRange(conversationHistory);

            // Create the user message.
            var userChatMessage = ChatMessage.CreateUserMessage(message);
            conversationForChat.Add(userChatMessage);

            // Also add the user message to the conversation history for persistence.
            conversationHistory.Add(userChatMessage);

            // Set up chat options.
            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1200,
                Temperature = 0.6f,
            };

            // Call the chat API.
            var response = await _openAIClient.CompleteChatAsync(conversationForChat, options);
            var assistantMessages = response.Value.Content;
            var assistantResponse = assistantMessages.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(assistantResponse))
            {
                throw new Exception("No assistant response received.");
            }

            // Log token usage.
            var usage = response.Value.Usage;
            _logger.LogInformation("Chat used [{totalTokens}] Total ~ [{input}] Input ~ [{output}] Output Tokens",
                usage.TotalTokenCount, usage.InputTokenCount, usage.OutputTokenCount);

            // Append the assistant response to the conversation history.
            conversationHistory.Add(ChatMessage.CreateAssistantMessage(assistantResponse));

            // Prepare persistent chat exchange messages.
            var modelMessages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    ChannelId = channelId,
                    Content = message,
                    IsUser = true,
                    AuthorId = userId,
                    CreatedAt = DateTime.UtcNow,
                },
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    ChannelId = channelId,
                    Content = assistantResponse,
                    IsUser = false,
                    AuthorId = botId,
                    CreatedAt = DateTime.UtcNow,
                }
            };

            // Update the cache/persistent storage with the new exchange.
            await _messageCacheService.AddChatExchange(channelId, conversationHistory, modelMessages);

            // Optimize message history if token limits are exceeded.
            if (usage.TotalTokenCount > MAX_TOKENS_TOTAL)
            {
                _logger.LogWarning("Chat used {totalTokens} tokens while the limit is {limit}, performing optimization",
                    usage.TotalTokenCount, MAX_TOKENS_TOTAL);

                int messagesToRemoveCount = await OptimizeMessagesAsync(usage.TotalTokenCount, conversationForChat);
                _messageCacheService.ClearOldMessages(channelId, messagesToRemoveCount);
            }
            return assistantResponse;
        }
        finally
        {
            channelSemaphore.Release();
        }
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