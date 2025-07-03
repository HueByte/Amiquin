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
        if (messages.Count % 2 == 0)
        {
            _logger.LogWarning("Even number of messages in the conversation history. Probably missing a persona message.");
        }

        foreach (var message in messages)
        {
            var text = message.Content.First().Text;
            int tokenCount = await Tokenizer.CountTokensAsync(text);

            // messagesToRemoveCount should be odd, since we include the persona message and user - assistant pair messages.
            if (currentTokenCount < targetTokenCount && messagesToRemoveCount % 2 != 0)
            {
                break;
            }

            currentTokenCount -= tokenCount;
            messagesToRemoveCount++;
        }

        // Skip the persona message as its developer message.
        var messagesToRemove = messages.Skip(1).Take(messagesToRemoveCount).ToList();
        var summary = await SummarizeMessagesAsync(messagesToRemove);

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

        sb.AppendLine("Amiquin, you’re nearing your memory limit. Summarize the following messages from your perspective—focus on key points, user requests, and relevant context, so you can carry on the conversation seamlessly. Keep the recap concise and in your own notes-style format.");
        foreach (var message in messages)
        {
            sb.AppendLine(message.Content.First().Text);
        }

        var response = await _chatCoreService.ExchangeMessageAsync(sb.ToString(), personaMessage, _botOptions.MaxTokens / 4);

        return response;
    }
}