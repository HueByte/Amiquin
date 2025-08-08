using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manages Discord bot chat sessions with in-memory storage and persistence.
/// </summary>
public class SessionManager : ISessionManager
{
    private readonly ILogger<SessionManager> _logger;
    private readonly ConcurrentDictionary<string, ChatSession> _sessions;

    public SessionManager(ILogger<SessionManager> logger)
    {
        _logger = logger;
        _sessions = new ConcurrentDictionary<string, ChatSession>();
    }

    public Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId)
    {
        var session = _sessions.GetOrAdd(sessionId, _ => new ChatSession
        {
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            MessageCount = 0
        });

        // Update last activity
        session.LastActivityAt = DateTime.UtcNow;

        _logger.LogDebug("Retrieved session {SessionId} for user {UserId}", sessionId, userId);
        return Task.FromResult(session);
    }

    public Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<bool> SessionExistsAsync(string sessionId)
    {
        return Task.FromResult(_sessions.ContainsKey(sessionId));
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        _logger.LogInformation("Deleted session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId)
    {
        var userSessions = _sessions.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastActivityAt)
            .AsEnumerable();
        
        return Task.FromResult(userSessions);
    }

    public Task UpdateSessionActivityAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.LastActivityAt = DateTime.UtcNow;
            session.MessageCount++;
        }
        
        return Task.CompletedTask;
    }

    public Task<int> GetActiveSessionCountAsync()
    {
        // Consider sessions active if they had activity in the last hour
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var activeCount = _sessions.Values.Count(s => s.LastActivityAt > cutoff);
        
        return Task.FromResult(activeCount);
    }

    public Task CleanupInactiveSessionsAsync(TimeSpan inactivityThreshold)
    {
        var cutoff = DateTime.UtcNow - inactivityThreshold;
        var inactiveSessions = _sessions.Where(kvp => kvp.Value.LastActivityAt < cutoff).ToList();

        foreach (var kvp in inactiveSessions)
        {
            _sessions.TryRemove(kvp.Key, out _);
            _logger.LogDebug("Cleaned up inactive session {SessionId}", kvp.Key);
        }

        if (inactiveSessions.Any())
        {
            _logger.LogInformation("Cleaned up {Count} inactive sessions", inactiveSessions.Count);
        }

        return Task.CompletedTask;
    }
}

public interface ISessionManager
{
    Task<ChatSession> GetOrCreateSessionAsync(string sessionId, string userId);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<bool> SessionExistsAsync(string sessionId);
    Task DeleteSessionAsync(string sessionId);
    Task<IEnumerable<ChatSession>> GetUserSessionsAsync(string userId);
    Task UpdateSessionActivityAsync(string sessionId);
    Task<int> GetActiveSessionCountAsync();
    Task CleanupInactiveSessionsAsync(TimeSpan inactivityThreshold);
}

public class ChatSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public int MessageCount { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}