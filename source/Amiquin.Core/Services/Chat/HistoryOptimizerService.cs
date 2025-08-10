using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Model;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text;

namespace Amiquin.Core.Services.Chat;

public class HistoryOptimizerService : IHistoryOptimizerService
{
    private readonly ILogger<HistoryOptimizerService> _logger;
    private readonly IChatCoreService _chatCoreService;
    private readonly BotOptions _botOptions;

    public HistoryOptimizerService(ILogger<HistoryOptimizerService> logger, IChatCoreService chatCoreService, IOptions<BotOptions> botOptions)
    {
        _logger = logger;
        _chatCoreService = chatCoreService;
        _botOptions = botOptions.Value;
    }

    public bool ShouldOptimizeMessageHistory(ChatTokenUsage tokenUsage)
    {
        return tokenUsage.TotalTokenCount - (tokenUsage.InputTokenDetails.CachedTokenCount / 2) > _botOptions.MaxTokens;
    }

    public async Task<OptimizerResult> OptimizeMessageHistory(int currentTokenCount, List<ChatMessage> messages, ChatMessage? personaMessage = null)
    {
        int targetTokenCount = _botOptions.MaxTokens / 2;
        int messagesToRemoveCount = 0;

        _logger.LogInformation("Starting message history optimization | Current token count: {currentTokenCount} | Target token count: {targetTokenCount}", currentTokenCount, targetTokenCount);
        
        // Account for persona message token count in optimization
        if (personaMessage != null)
        {
            var personaText = personaMessage.Content.First().Text;
            int personaTokenCount = await Tokenizer.CountTokensAsync(personaText);
            currentTokenCount += personaTokenCount; // Add persona tokens to current count
        }

        // Messages should be user-assistant pairs (even number)
        if (messages.Count % 2 != 0)
        {
            _logger.LogWarning("Odd number of messages in the conversation history. Expected user-assistant pairs.");
        }

        foreach (var message in messages)
        {
            var text = message.Content.First().Text;
            int tokenCount = await Tokenizer.CountTokensAsync(text);

            // Remove complete user-assistant pairs (even messagesToRemoveCount)
            if (currentTokenCount < targetTokenCount && messagesToRemoveCount % 2 == 0 && messagesToRemoveCount > 0)
            {
                break;
            }

            currentTokenCount -= tokenCount;
            messagesToRemoveCount++;
        }

        // Ensure we remove complete pairs
        if (messagesToRemoveCount % 2 != 0)
        {
            messagesToRemoveCount--;
        }

        var messagesToRemove = messages.Take(messagesToRemoveCount).ToList();
        var summary = await SummarizeMessagesAsync(messagesToRemove, personaMessage);

        _logger.LogInformation("Message history optimization completed | Messages removed: {messagesToRemoveCount} | New token count: {currentTokenCount}\nPerformed summary {summary}", messagesToRemoveCount, currentTokenCount, summary);

        return new OptimizerResult()
        {
            RemovedMessages = messagesToRemoveCount,
            MessagesSummary = summary,
        };
    }

    private async Task<string> SummarizeMessagesAsync(List<ChatMessage> messages, ChatMessage? personaMessage = null)
    {
        StringBuilder sb = new();

        sb.AppendLine("Summarize the following conversation messages into a concise context summary. Focus on key points, user requests, decisions made, and important information that would be needed to continue the conversation naturally. Keep it objective and factual:");
        
        foreach (var message in messages)
        {
            var text = message.Content.First().Text;
            // Determine role based on message content patterns or add role tracking
            var role = message.Content.First().Kind.ToString() == "User" ? "User" : "Assistant";
            sb.AppendLine($"{role}: {text}");
        }

        // Use pure summarization without persona for context generation
        var neutralSystemMessage = ChatMessage.CreateSystemMessage("You are a helpful assistant that creates concise, objective summaries of conversations.");
        var response = await _chatCoreService.ExchangeMessageAsync(sb.ToString(), neutralSystemMessage, _botOptions.MaxTokens / 4);

        return response;
    }
}