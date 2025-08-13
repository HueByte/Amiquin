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