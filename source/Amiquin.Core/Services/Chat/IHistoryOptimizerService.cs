using Amiquin.Core.Services.Chat.Model;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public interface IHistoryOptimizerService
{
    Task<OptimizerResult> OptimizeMessageHistory(int currentTokenCount, List<ChatMessage> messages, ChatMessage? personaMessage = null);
    bool ShouldOptimizeMessageHistory(ChatTokenUsage tokenUsage);
}
