using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public class PersonaChatService : IPersonaChatService
{
    private readonly ILogger<PersonaChatService> _logger;
    private readonly IChatCoreService _chatCoreService;
    private readonly IPersonaService _personaService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IHistoryOptimizerService _historyOptimizerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotOptions _botOptions;
    private const float PRICING_OUTPUT = 0.600f;
    private const float PRICING_INPUT = 0.150f;
    private const float PRICING_INPUT_CACHED = 0.075f;
    private const float ONE_MILLION = 1_000_000;

    public PersonaChatService(ILogger<PersonaChatService> logger, IChatCoreService chatCoreService, IPersonaService personaService, IMessageCacheService messageCacheService, IHistoryOptimizerService historyOptimizerService, IServiceProvider serviceProvider, IOptions<BotOptions> botOptions)
    {
        _logger = logger;
        _chatCoreService = chatCoreService;
        _personaService = personaService;
        _messageCacheService = messageCacheService;
        _historyOptimizerService = historyOptimizerService;
        _serviceProvider = serviceProvider;
        _botOptions = botOptions.Value;
    }

    public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
    {
        // Get session and persona 
        var chatSessionService = _serviceProvider.GetRequiredService<IChatSessionService>();
        var session = await chatSessionService.GetOrCreateServerSessionAsync(instanceId);
        var persona = await _personaService.GetPersonaAsync(instanceId);

        // Create system message with context appended if it exists
        var systemMessage = CreateSystemMessageWithContext(persona, session.Context);

        var conversationHistory = await PrepareMessageHistory(instanceId, message);

        // Pass conversation history to ChatCoreService, which will handle system message internally
        var response = await _chatCoreService.ChatAsync(instanceId, conversationHistory, systemMessage);
        var assistantMessages = response.Content;
        var assistantResponse = assistantMessages.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(assistantResponse))
        {
            throw new Exception("No assistant response received.");
        }

        var tokenUsage = response.Usage;
        LogTokenUsage(tokenUsage);

        var userMessage = conversationHistory.Last();
        var amiquinMessage = ChatMessage.CreateAssistantMessage(assistantResponse);

        var models = CreateMessageModels(instanceId, botId, userId, userMessage, amiquinMessage);

        // Only add the new message exchange (user + assistant), not the entire conversation history
        var newMessageExchange = new List<ChatMessage> { userMessage, amiquinMessage };
        await _messageCacheService.AddChatExchangeAsync(instanceId, newMessageExchange, models);

        // For optimization, we need the full conversation with the assistant response added
        var fullConversationWithResponse = new List<ChatMessage>(conversationHistory) { amiquinMessage };

        if (_historyOptimizerService.ShouldOptimizeMessageHistory(tokenUsage))
        {
            var optimizationResult = await _historyOptimizerService.OptimizeMessageHistory(tokenUsage.TotalTokenCount, fullConversationWithResponse, systemMessage);

            // Clear old messages from cache (they stay in DB)
            _messageCacheService.ClearOldMessages(instanceId, optimizationResult.RemovedMessages);

            // Update session context with the summary
            var chatSessionRepository = _serviceProvider.GetRequiredService<IChatSessionRepository>();
            var contextTokens = await Tokenizer.CountTokensAsync(optimizationResult.MessagesSummary);

            // Check if context itself is getting too big and needs self-summarization
            if (session.ContextTokens + contextTokens > _botOptions.MaxTokens / 4) // 25% of max tokens
            {
                var selfSummarizedContext = await SelfSummarizeContext(session.Context, optimizationResult.MessagesSummary, persona);
                var selfSummarizedTokens = await Tokenizer.CountTokensAsync(selfSummarizedContext);
                await chatSessionRepository.UpdateSessionContextAsync(session.Id, selfSummarizedContext, selfSummarizedTokens);
            }
            else
            {
                // Append new summary to existing context
                var newContext = string.IsNullOrEmpty(session.Context)
                    ? optimizationResult.MessagesSummary
                    : $"{session.Context}\n\n{optimizationResult.MessagesSummary}";
                await chatSessionRepository.UpdateSessionContextAsync(session.Id, newContext, session.ContextTokens + contextTokens);
            }
        }

        return assistantResponse;
    }

    public async Task<string> ExchangeMessageAsync(ulong instanceId, string message)
    {
        var persona = await _personaService.GetPersonaAsync(instanceId);
        return await _chatCoreService.ExchangeMessageAsync(message, persona, tokenLimit: 1200, instanceId: instanceId);
    }

    private void LogTokenUsage(ChatTokenUsage usage)
    {
        _logger.LogInformation("Chat used [Total: {totalTokens}] ~ [Input: {inputTokens}] ~ [CachedInput: {cachedInputTokens}] ~ [Output: {outputTokens}] ~ [AmiquinCounter: {amiquinTokens}] tokens",
            usage.TotalTokenCount, usage.InputTokenCount, usage.InputTokenDetails.CachedTokenCount, usage.OutputTokenCount, usage.TotalTokenCount - (usage.InputTokenDetails.CachedTokenCount / 2));

        _logger.LogInformation("Estimated message price: {messagePrice}$", CalculateMessagePrice(usage));
    }

    private async Task<List<ChatMessage>> PrepareMessageHistory(ulong instanceId, string message)
    {
        var cachedMessages = await _messageCacheService.GetOrCreateChatMessagesAsync(instanceId)
                             ?? [];

        var conversationHistory = new List<ChatMessage>(cachedMessages);

        // Create the user message and add it to the conversation
        var userChatMessage = ChatMessage.CreateUserMessage(message);
        conversationHistory.Add(userChatMessage);

        return conversationHistory;
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
                    ServerId = instanceId,
                    Content = userMessage.Content.FirstOrDefault()?.Text ?? string.Empty,
                    IsUser = true,
                    AuthorId = userId,
                    CreatedAt = DateTime.UtcNow,
                },
                new Message
                {
                    Id = Guid.NewGuid().ToString(),
                    ServerId = instanceId,
                    Content = amiquinMessage.Content.FirstOrDefault()?.Text ?? string.Empty,
                    IsUser = false,
                    AuthorId = botId,
                    CreatedAt = DateTime.UtcNow,
                }
            };

        return modelMessages;
    }

    /// <summary>
    /// Creates a system message with context appended if it exists
    /// </summary>
    private static ChatMessage CreateSystemMessageWithContext(string persona, string? context)
    {
        var systemContent = persona;

        if (!string.IsNullOrEmpty(context))
        {
            systemContent += $"\n\nPrevious conversation context:\n{context}";
        }

        return ChatMessage.CreateSystemMessage(systemContent);
    }

    /// <summary>
    /// Self-summarizes the context when it gets too big
    /// </summary>
    private async Task<string> SelfSummarizeContext(string? existingContext, string newSummary, string persona)
    {
        var contextToSummarize = string.IsNullOrEmpty(existingContext)
            ? newSummary
            : $"{existingContext}\n\n{newSummary}";

        var summarizationPrompt = $"You're managing a conversation context that's getting too long. Consolidate the following context summaries into one concise summary that captures the most important points, relationships, and ongoing themes. Keep it under 400 tokens:\n\n{contextToSummarize}";

        return await _chatCoreService.ExchangeMessageAsync(summarizationPrompt, ChatMessage.CreateSystemMessage(persona), tokenLimit: 300);
    }
}