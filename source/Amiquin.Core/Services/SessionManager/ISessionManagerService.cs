using ChatSessionModel = Amiquin.Core.Models.ChatSession;

namespace Amiquin.Core.Services.SessionManager;

/// <summary>
/// Service for managing multiple chat sessions per server with session switching capabilities.
/// </summary>
public interface ISessionManagerService
{
    /// <summary>
    /// Gets all sessions for a server.
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <returns>List of all sessions for the server.</returns>
    Task<List<ChatSessionModel>> GetServerSessionsAsync(ulong serverId);

    /// <summary>
    /// Gets the currently active session for a server.
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <returns>Active session or null if none exists.</returns>
    Task<ChatSessionModel?> GetActiveSessionAsync(ulong serverId);

    /// <summary>
    /// Creates a new session for a server.
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <param name="sessionName">Name for the new session.</param>
    /// <param name="setAsActive">Whether to set this as the active session.</param>
    /// <param name="model">AI model to use.</param>
    /// <param name="provider">AI provider to use.</param>
    /// <returns>The created session.</returns>
    Task<ChatSessionModel> CreateSessionAsync(ulong serverId, string sessionName, bool setAsActive = true, string model = "gpt-4o-mini", string provider = "OpenAI");

    /// <summary>
    /// Switches the active session for a server.
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <param name="sessionId">ID of the session to make active.</param>
    /// <returns>True if successful, false if session not found.</returns>
    Task<bool> SwitchSessionAsync(ulong serverId, string sessionId);

    /// <summary>
    /// Renames a session.
    /// </summary>
    /// <param name="sessionId">Session ID to rename.</param>
    /// <param name="newName">New name for the session.</param>
    /// <returns>True if successful, false if session not found.</returns>
    Task<bool> RenameSessionAsync(string sessionId, string newName);

    /// <summary>
    /// Deletes a session (cannot delete the last remaining session).
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <param name="sessionId">Session ID to delete.</param>
    /// <returns>True if successful, false if session not found or is the last session.</returns>
    Task<bool> DeleteSessionAsync(ulong serverId, string sessionId);

    /// <summary>
    /// Gets session statistics for display.
    /// </summary>
    /// <param name="sessionId">Session ID.</param>
    /// <returns>Session stats or null if not found.</returns>
    Task<SessionStats?> GetSessionStatsAsync(string sessionId);

    /// <summary>
    /// Archives a session (makes it inactive but preserves it).
    /// </summary>
    /// <param name="sessionId">Session ID to archive.</param>
    /// <returns>True if successful.</returns>
    Task<bool> ArchiveSessionAsync(string sessionId);

    /// <summary>
    /// Checks if a session is stale (inactive for longer than timeout).
    /// </summary>
    /// <param name="sessionId">Session ID to check.</param>
    /// <param name="inactivityTimeoutMinutes">Minutes of inactivity before considered stale.</param>
    /// <returns>True if session is stale and needs refresh.</returns>
    Task<bool> IsSessionStaleAsync(string sessionId, int inactivityTimeoutMinutes = 30);

    /// <summary>
    /// Refreshes a stale session by archiving old messages and starting fresh,
    /// while preserving memories for continuity.
    /// </summary>
    /// <param name="serverId">Server ID.</param>
    /// <param name="userId">User ID triggering the refresh.</param>
    /// <returns>Refreshed session result with memory context.</returns>
    Task<SessionRefreshResult> RefreshStaleSessionAsync(ulong serverId, ulong userId);

    /// <summary>
    /// Compacts a session by converting old messages to memories and keeping only recent ones.
    /// </summary>
    /// <param name="sessionId">Session ID to compact.</param>
    /// <param name="messagesToKeep">Number of recent messages to keep.</param>
    /// <returns>Compaction result with number of messages archived.</returns>
    Task<SessionCompactionResult> CompactSessionAsync(string sessionId, int messagesToKeep = 10);

    /// <summary>
    /// Updates the last activity timestamp for a session.
    /// </summary>
    /// <param name="sessionId">Session ID to update.</param>
    Task UpdateSessionActivityAsync(string sessionId);

    /// <summary>
    /// Checks if a session needs compaction based on message count.
    /// </summary>
    /// <param name="sessionId">Session ID to check.</param>
    /// <param name="maxMessages">Maximum messages before compaction is needed.</param>
    /// <returns>True if compaction is needed.</returns>
    Task<bool> NeedsCompactionAsync(string sessionId, int maxMessages = 50);
}

/// <summary>
/// Result of a session refresh operation.
/// </summary>
public class SessionRefreshResult
{
    /// <summary>
    /// Whether the refresh was performed.
    /// </summary>
    public bool WasRefreshed { get; set; }

    /// <summary>
    /// The refreshed/current session ID.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Memory context retrieved for continuity.
    /// </summary>
    public string? MemoryContext { get; set; }

    /// <summary>
    /// Number of memories retrieved for context.
    /// </summary>
    public int MemoriesRetrieved { get; set; }

    /// <summary>
    /// Time since last activity before refresh.
    /// </summary>
    public TimeSpan InactivityDuration { get; set; }

    /// <summary>
    /// Summary created from previous session (if any).
    /// </summary>
    public string? PreviousSessionSummary { get; set; }
}

/// <summary>
/// Result of a session compaction operation.
/// </summary>
public class SessionCompactionResult
{
    /// <summary>
    /// Whether compaction was performed.
    /// </summary>
    public bool WasCompacted { get; set; }

    /// <summary>
    /// Number of messages archived/removed.
    /// </summary>
    public int MessagesArchived { get; set; }

    /// <summary>
    /// Number of messages kept.
    /// </summary>
    public int MessagesKept { get; set; }

    /// <summary>
    /// Number of memories created from archived messages.
    /// </summary>
    public int MemoriesCreated { get; set; }

    /// <summary>
    /// Summary of the compacted conversation (if created).
    /// </summary>
    public string? ConversationSummary { get; set; }
}

/// <summary>
/// Statistics information for a chat session.
/// </summary>
public class SessionStats
{
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public int MessageCount { get; set; }
    public int EstimatedTokens { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}