using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manages message history for Discord bot conversations with in-memory storage.
/// </summary>
public class MessageHistoryManager : IMessageHistoryManager
{
    private readonly ILogger<MessageHistoryManager> _logger;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _messageHistory;
    private readonly ConcurrentDictionary<string, SessionStats> _sessionStats;

    public MessageHistoryManager(ILogger<MessageHistoryManager> logger)
    {
        _logger = logger;
        _messageHistory = new ConcurrentDictionary<string, List<ChatMessage>>();
        _sessionStats = new ConcurrentDictionary<string, SessionStats>();
    }

    public Task<List<ChatMessage>> GetMessageHistoryAsync(string sessionId)
    {
        var messages = _messageHistory.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        
        // Return a copy to prevent external modifications
        var messagesCopy = new List<ChatMessage>(messages);
        
        _logger.LogDebug("Retrieved {Count} messages for session {SessionId}", messagesCopy.Count, sessionId);
        return Task.FromResult(messagesCopy);
    }

    public Task SaveMessageExchangeAsync(string sessionId, ChatMessage userMessage, ChatMessage assistantMessage)
    {
        var messages = _messageHistory.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        
        lock (messages)
        {
            messages.Add(userMessage);
            messages.Add(assistantMessage);
        }

        // Update session stats
        var stats = _sessionStats.GetOrAdd(sessionId, _ => new SessionStats 
        { 
            SessionId = sessionId,
            StartTime = DateTime.UtcNow
        });

        stats.MessageCount += 2; // User + Assistant message
        stats.LastMessageTime = DateTime.UtcNow;
        stats.TotalTokensUsed += EstimateTokenCount(userMessage.Content.First().Text) + 
                                EstimateTokenCount(assistantMessage.Content.First().Text);

        _logger.LogDebug("Saved message exchange for session {SessionId}. Total messages: {Count}", 
            sessionId, messages.Count);
        
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(string sessionId, ChatMessage message)
    {
        var messages = _messageHistory.GetOrAdd(sessionId, _ => new List<ChatMessage>());
        
        lock (messages)
        {
            messages.Add(message);
        }

        // Update stats
        if (_sessionStats.TryGetValue(sessionId, out var stats))
        {
            stats.MessageCount++;
            stats.LastMessageTime = DateTime.UtcNow;
            stats.TotalTokensUsed += EstimateTokenCount(message.Content.First().Text);
        }

        _logger.LogDebug("Added message to session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task<int> GetMessageCountAsync(string sessionId)
    {
        if (_messageHistory.TryGetValue(sessionId, out var messages))
        {
            return Task.FromResult(messages.Count);
        }
        
        return Task.FromResult(0);
    }

    public Task ClearHistoryAsync(string sessionId)
    {
        _messageHistory.TryRemove(sessionId, out _);
        _sessionStats.TryRemove(sessionId, out _);
        
        _logger.LogInformation("Cleared message history for session {SessionId}", sessionId);
        return Task.CompletedTask;
    }

    public Task OptimizeHistoryAsync(string sessionId, int messagesToRemove)
    {
        if (_messageHistory.TryGetValue(sessionId, out var messages))
        {
            lock (messages)
            {
                if (messagesToRemove > 0 && messagesToRemove < messages.Count)
                {
                    // Remove the oldest messages, but keep the system message if it's first
                    var systemMessageCount = messages.Count > 0 && messages[0].Role == ChatMessageRole.System ? 1 : 0;
                    var removeFrom = systemMessageCount;
                    var removeCount = Math.Min(messagesToRemove, messages.Count - systemMessageCount);
                    
                    messages.RemoveRange(removeFrom, removeCount);
                    
                    _logger.LogInformation("Optimized history for session {SessionId}: removed {Count} messages", 
                        sessionId, removeCount);
                }
            }
        }
        
        return Task.CompletedTask;
    }

    public Task<SessionStats> GetSessionStatsAsync(string sessionId)
    {
        var stats = _sessionStats.GetOrAdd(sessionId, _ => new SessionStats 
        { 
            SessionId = sessionId,
            StartTime = DateTime.UtcNow,
            MessageCount = 0,
            TotalTokensUsed = 0
        });
        
        return Task.FromResult(stats);
    }

    public Task<List<string>> GetActiveSessionsAsync()
    {
        var activeCutoff = DateTime.UtcNow.AddHours(-2);
        var activeSessions = _sessionStats.Values
            .Where(s => s.LastMessageTime > activeCutoff)
            .Select(s => s.SessionId)
            .ToList();
            
        return Task.FromResult(activeSessions);
    }

    public Task CleanupOldHistoryAsync(TimeSpan retentionPeriod)
    {
        var cutoff = DateTime.UtcNow - retentionPeriod;
        var expiredSessions = _sessionStats.Where(kvp => kvp.Value.LastMessageTime < cutoff).ToList();

        foreach (var kvp in expiredSessions)
        {
            _messageHistory.TryRemove(kvp.Key, out _);
            _sessionStats.TryRemove(kvp.Key, out _);
        }

        if (expiredSessions.Any())
        {
            _logger.LogInformation("Cleaned up {Count} expired message histories", expiredSessions.Count);
        }

        return Task.CompletedTask;
    }

    private int EstimateTokenCount(string text)
    {
        // Simple token estimation: roughly 4 characters per token
        return text.Length / 4;
    }
}

public interface IMessageHistoryManager
{
    Task<List<ChatMessage>> GetMessageHistoryAsync(string sessionId);
    Task SaveMessageExchangeAsync(string sessionId, ChatMessage userMessage, ChatMessage assistantMessage);
    Task AddMessageAsync(string sessionId, ChatMessage message);
    Task<int> GetMessageCountAsync(string sessionId);
    Task ClearHistoryAsync(string sessionId);
    Task OptimizeHistoryAsync(string sessionId, int messagesToRemove);
    Task<SessionStats> GetSessionStatsAsync(string sessionId);
    Task<List<string>> GetActiveSessionsAsync();
    Task CleanupOldHistoryAsync(TimeSpan retentionPeriod);
}

public class SessionStats
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime LastMessageTime { get; set; }
    public int MessageCount { get; set; }
    public int TotalTokensUsed { get; set; }
}