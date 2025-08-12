using Amiquin.Core.Abstraction;
using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for managing SessionMessage entities.
/// Provides access to messages within chat sessions.
/// </summary>
public interface ISessionMessageRepository : IRepository<string, SessionMessage>
{
    /// <summary>
    /// Gets messages for a specific chat session that should be included in context.
    /// </summary>
    /// <param name="chatSessionId">The chat session ID</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 50)</param>
    /// <returns>List of session messages ordered by creation date (oldest first)</returns>
    Task<List<SessionMessage>> GetSessionContextMessagesAsync(string chatSessionId, int limit = 50);

    /// <summary>
    /// Gets all messages for a specific chat session.
    /// </summary>
    /// <param name="chatSessionId">The chat session ID</param>
    /// <param name="limit">Maximum number of messages to retrieve (default: 100)</param>
    /// <returns>List of session messages ordered by creation date (oldest first)</returns>
    Task<List<SessionMessage>> GetSessionMessagesAsync(string chatSessionId, int limit = 100);

    /// <summary>
    /// Adds a new message to a chat session.
    /// </summary>
    /// <param name="chatSessionId">The chat session ID</param>
    /// <param name="role">The role of the message sender (user, assistant, system)</param>
    /// <param name="content">The message content</param>
    /// <param name="discordMessageId">Optional Discord message ID</param>
    /// <param name="estimatedTokens">Estimated token count for the message</param>
    /// <param name="includeInContext">Whether to include this message in context (default: true)</param>
    /// <returns>The created SessionMessage</returns>
    Task<SessionMessage> AddSessionMessageAsync(
        string chatSessionId,
        string role,
        string content,
        string? discordMessageId = null,
        int estimatedTokens = 0,
        bool includeInContext = true);

    /// <summary>
    /// Gets the total count of messages in a chat session.
    /// </summary>
    /// <param name="chatSessionId">The chat session ID</param>
    /// <returns>Total count of messages in the session</returns>
    Task<int> GetSessionMessageCountAsync(string chatSessionId);

    /// <summary>
    /// Removes old messages from a session to manage context size.
    /// Keeps the most recent messages based on the limit.
    /// </summary>
    /// <param name="chatSessionId">The chat session ID</param>
    /// <param name="keepCount">Number of most recent messages to keep</param>
    /// <returns>Number of messages removed</returns>
    Task<int> TrimSessionMessagesAsync(string chatSessionId, int keepCount);
}