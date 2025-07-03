using Amiquin.Core.Services.Chat.Model;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Service interface for optimizing chat message history to manage token usage.
/// Provides methods for analyzing and reducing message history when token limits are approached.
/// </summary>
public interface IHistoryOptimizerService
{
    /// <summary>
    /// Optimizes message history by reducing token count while preserving important context.
    /// </summary>
    /// <param name="currentTokenCount">The current token count of the message history.</param>
    /// <param name="messages">The list of chat messages to optimize.</param>
    /// <param name="personaMessage">Optional persona message to consider during optimization.</param>
    /// <returns>An OptimizerResult containing the optimized message list and metadata.</returns>
    Task<OptimizerResult> OptimizeMessageHistory(int currentTokenCount, List<ChatMessage> messages, ChatMessage? personaMessage = null);

    /// <summary>
    /// Determines whether message history optimization should be performed based on token usage.
    /// </summary>
    /// <param name="tokenUsage">The current token usage statistics.</param>
    /// <returns>True if optimization should be performed; otherwise, false.</returns>
    bool ShouldOptimizeMessageHistory(ChatTokenUsage tokenUsage);
}