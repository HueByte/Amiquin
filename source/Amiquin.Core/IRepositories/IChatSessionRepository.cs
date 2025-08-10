using Amiquin.Core.Abstraction;
using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for managing chat sessions
/// </summary>
public interface IChatSessionRepository : IQueryableRepository<string, ChatSession>
{
    /// <summary>
    /// Gets an active chat session for the specified scope and context
    /// </summary>
    /// <param name="scope">The session scope (User, Channel, Server)</param>
    /// <param name="userId">Discord User ID</param>
    /// <param name="channelId">Discord Channel ID</param>
    /// <param name="serverId">Discord Server ID</param>
    /// <returns>Active chat session if found, null otherwise</returns>
    Task<ChatSession?> GetActiveSessionAsync(SessionScope scope, ulong userId = 0, ulong channelId = 0, ulong serverId = 0);

    /// <summary>
    /// Creates or gets an existing active chat session for the specified scope and context
    /// </summary>
    /// <param name="scope">The session scope (User, Channel, Server)</param>
    /// <param name="userId">Discord User ID</param>
    /// <param name="channelId">Discord Channel ID</param>
    /// <param name="serverId">Discord Server ID</param>
    /// <param name="model">AI model to use for new sessions</param>
    /// <param name="provider">AI provider to use for new sessions</param>
    /// <returns>Active chat session</returns>
    Task<ChatSession> GetOrCreateActiveSessionAsync(SessionScope scope, ulong userId = 0, ulong channelId = 0, ulong serverId = 0, string model = "gpt-4o-mini", string provider = "OpenAI");

    /// <summary>
    /// Updates the model for an existing chat session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="model">New model name</param>
    /// <param name="provider">New provider name</param>
    /// <returns>Updated session if found, null otherwise</returns>
    Task<ChatSession?> UpdateSessionModelAsync(string sessionId, string model, string provider);

    /// <summary>
    /// Updates the model for sessions matching the specified scope and context
    /// </summary>
    /// <param name="scope">The session scope (User, Channel, Server)</param>
    /// <param name="model">New model name</param>
    /// <param name="provider">New provider name</param>
    /// <param name="userId">Discord User ID (for User scope)</param>
    /// <param name="channelId">Discord Channel ID (for Channel scope)</param>
    /// <param name="serverId">Discord Server ID (for Server scope)</param>
    /// <returns>Number of sessions updated</returns>
    Task<int> UpdateSessionModelByScopeAsync(SessionScope scope, string model, string provider, ulong userId = 0, ulong channelId = 0, ulong serverId = 0);

    /// <summary>
    /// Deactivates old sessions to keep only the most recent ones
    /// </summary>
    /// <param name="scope">The session scope</param>
    /// <param name="owningEntityId">The owning entity ID</param>
    /// <param name="keepCount">Number of recent sessions to keep active</param>
    /// <returns>Number of sessions deactivated</returns>
    Task<int> DeactivateOldSessionsAsync(SessionScope scope, ulong owningEntityId, int keepCount = 5);

    /// <summary>
    /// Updates the context summary for a chat session
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="context">New context summary</param>
    /// <param name="contextTokens">Estimated token count for the context</param>
    /// <returns>Updated session if found, null otherwise</returns>
    Task<ChatSession?> UpdateSessionContextAsync(string sessionId, string context, int contextTokens);
}
