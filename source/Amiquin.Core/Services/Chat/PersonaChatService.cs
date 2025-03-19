using Amiquin.Core.Models;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Persona;
using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public class PersonaChatService : IPersonaChatService
{
    private readonly ILogger<PersonaChatService> _logger;
    private readonly IChatCoreService _chatCoreService;
    private readonly IPersonaService _personaService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IHistoryOptimizerService _historyOptimizerService;
    private const float PRICING_OUTPUT = 0.600f;
    private const float PRICING_INPUT = 0.150f;
    private const float PRICING_INPUT_CACHED = 0.075f;
    private const float ONE_MILLION = 1_000_000;

    public PersonaChatService(ILogger<PersonaChatService> logger, IChatCoreService chatCoreService, IPersonaService personaService, IMessageCacheService messageCacheService, IHistoryOptimizerService historyOptimizerService)
    {
        _logger = logger;
        _chatCoreService = chatCoreService;
        _personaService = personaService;
        _messageCacheService = messageCacheService;
        _historyOptimizerService = historyOptimizerService;
    }

    public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
    {
        // The conversationForChat contains persona message
        var persona = await _personaService.GetPersonaAsync(instanceId);
        var (conversationForChat, conversationHistory) = await PrepareMessageHistory(instanceId, message);

        var response = await _chatCoreService.ChatAsync(instanceId, conversationForChat, ChatMessage.CreateDeveloperMessage(persona));
        var assistantMessages = response.Content;
        var assistantResponse = assistantMessages.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(assistantResponse))
        {
            throw new Exception("No assistant response received.");
        }

        var tokenUsage = response.Usage;
        LogTokenUsage(tokenUsage);

        var userMessage = conversationForChat.Last();
        var amiquinMessage = ChatMessage.CreateAssistantMessage(assistantResponse);

        conversationForChat.Add(amiquinMessage);
        conversationHistory.Add(amiquinMessage);

        var models = CreateMessageModels(instanceId, botId, userId, userMessage, amiquinMessage);

        await _messageCacheService.AddChatExchangeAsync(instanceId, conversationHistory, models);

        if (_historyOptimizerService.ShouldOptimizeMessageHistory(tokenUsage))
        {
            var optimizationResult = await _historyOptimizerService.OptimizeMessageHistory(tokenUsage.TotalTokenCount, conversationForChat, persona);

            // Subtract one from the removed messages as optimization was performed with Persona messagee
            // We ensure with that, that user-assistant message pair is removed.
            _messageCacheService.ClearOldMessages(instanceId, optimizationResult.RemovedMessages - 1);
            await _personaService.AddSummaryAsync(optimizationResult.MessagesSummary);
        }

        return assistantResponse;
    }

    public async Task<string> ExchangeMessageAsync(string message)
    {
        var persona = await _personaService.GetPersonaAsync();
        return await _chatCoreService.ExchangeMessageAsync(message, persona);
    }

    private void LogTokenUsage(ChatTokenUsage usage)
    {
        _logger.LogInformation("Chat used [Total: {totalTokens}] ~ [Input: {inputTokens}] ~ [CachedInput: {cachedInputTokens}] ~ [Output: {outputTokens}] tokens",
            usage.TotalTokenCount, usage.InputTokenCount, usage.InputTokenDetails.CachedTokenCount, usage.OutputTokenCount);

        _logger.LogInformation("Estimated message price: {messagePrice}$", CalculateMessagePrice(usage));
    }

    private async Task<(List<ChatMessage>, List<ChatMessage>)> PrepareMessageHistory(ulong instanceId, string message)
    {
        var cachedMessages = await _messageCacheService.GetOrCreateChatMessagesAsync(instanceId)
                             ?? [];

        var conversationHistory = new List<ChatMessage>(cachedMessages);

        var conversationForChat = new List<ChatMessage>();
        conversationForChat.AddRange(conversationHistory);

        // Create the user message.
        var userChatMessage = ChatMessage.CreateUserMessage(message);
        conversationForChat.Add(userChatMessage);

        // Also add the user message to the conversation history for persistence.
        conversationHistory.Add(userChatMessage);

        return (conversationForChat, conversationHistory);
    }

    private float CalculateMessagePrice(ChatTokenUsage tokenUsage)
    {
        var messagePrice =
            (tokenUsage.InputTokenCount - tokenUsage.InputTokenDetails.CachedTokenCount) / ONE_MILLION * PRICING_INPUT
            + tokenUsage.InputTokenDetails.CachedTokenCount / ONE_MILLION * PRICING_INPUT_CACHED
            + tokenUsage.OutputTokenCount / ONE_MILLION * PRICING_OUTPUT;

        return messagePrice;
    }

    private List<Message> CreateMessageModels(ulong instanceId, ulong botId, ulong userId, ChatMessage userMessage, ChatMessage amiquinMessage)
    {
        var modelMessages = new List<Message>
            {
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    InstanceId = instanceId,
                    Content = userMessage.Content.FirstOrDefault()?.Text ?? string.Empty,
                    IsUser = true,
                    AuthorId = userId,
                    CreatedAt = DateTime.UtcNow,
                },
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    InstanceId = instanceId,
                    Content = amiquinMessage.Content.FirstOrDefault()?.Text ?? string.Empty,
                    IsUser = false,
                    AuthorId = botId,
                    CreatedAt = DateTime.UtcNow,
                }
            };

        return modelMessages;
    }
}