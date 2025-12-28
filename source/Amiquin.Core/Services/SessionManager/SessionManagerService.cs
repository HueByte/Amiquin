using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Memory;
using Amiquin.Core.Services.MessageCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ChatSessionModel = Amiquin.Core.Models.ChatSession;

namespace Amiquin.Core.Services.SessionManager;

/// <summary>
/// Service implementation for managing multiple chat sessions per server.
/// </summary>
public class SessionManagerService : ISessionManagerService
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly ISessionMessageRepository _sessionMessageRepository;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IMemoryService? _memoryService;
    private readonly ILogger<SessionManagerService> _logger;
    private readonly MemoryOptions _memoryOptions;

    public SessionManagerService(
        IChatSessionRepository sessionRepository,
        ISessionMessageRepository sessionMessageRepository,
        IMessageCacheService messageCacheService,
        ILogger<SessionManagerService> logger,
        IOptions<MemoryOptions> memoryOptions,
        IMemoryService? memoryService = null)
    {
        _sessionRepository = sessionRepository;
        _sessionMessageRepository = sessionMessageRepository;
        _messageCacheService = messageCacheService;
        _memoryService = memoryService;
        _logger = logger;
        _memoryOptions = memoryOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<List<ChatSessionModel>> GetServerSessionsAsync(ulong serverId)
    {
        return await _sessionRepository.AsQueryable()
            .Where(s => s.Scope == SessionScope.Server && s.OwningEntityId == serverId)
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ChatSessionModel?> GetActiveSessionAsync(ulong serverId)
    {
        return await _sessionRepository.AsQueryable()
            .FirstOrDefaultAsync(s => s.Scope == SessionScope.Server &&
                                     s.OwningEntityId == serverId &&
                                     s.IsActive);
    }

    /// <inheritdoc/>
    public async Task<ChatSessionModel> CreateSessionAsync(ulong serverId, string sessionName, bool setAsActive = true, string model = "gpt-4o-mini", string provider = "OpenAI")
    {
        if (string.IsNullOrWhiteSpace(sessionName))
            throw new ArgumentException("Session name cannot be empty", nameof(sessionName));

        // Check if a session with this name already exists
        var existingSession = await _sessionRepository.AsQueryable()
            .FirstOrDefaultAsync(s => s.Scope == SessionScope.Server &&
                                     s.OwningEntityId == serverId &&
                                     s.Name == sessionName);

        if (existingSession != null)
            throw new InvalidOperationException($"A session with the name '{sessionName}' already exists");

        // If setting as active, deactivate other sessions
        if (setAsActive)
        {
            await DeactivateAllSessionsAsync(serverId);

            // Clear the message cache to refresh with the new session's messages
            _messageCacheService.ClearServerMessageCache(serverId);
        }

        // Create the new session
        var newSession = ChatSessionModel.CreateSession(
            scope: SessionScope.Server,
            serverId: serverId,
            model: model,
            provider: provider,
            name: sessionName);

        newSession.IsActive = setAsActive;

        await _sessionRepository.AddAsync(newSession);
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation("Created new session '{SessionName}' for server {ServerId}", sessionName, serverId);

        return newSession;
    }

    /// <inheritdoc/>
    public async Task<bool> SwitchSessionAsync(ulong serverId, string sessionId)
    {
        var targetSession = await _sessionRepository.AsQueryable()
            .FirstOrDefaultAsync(s => s.Id == sessionId &&
                                     s.Scope == SessionScope.Server &&
                                     s.OwningEntityId == serverId);

        if (targetSession == null)
        {
            _logger.LogWarning("Attempted to switch to non-existent session {SessionId} on server {ServerId}", sessionId, serverId);
            return false;
        }

        // Deactivate all other sessions for this server
        await DeactivateAllSessionsAsync(serverId);

        // Clear the message cache to refresh with the new session's messages
        _messageCacheService.ClearServerMessageCache(serverId);

        // Activate the target session
        targetSession.IsActive = true;
        targetSession.LastActivityAt = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(targetSession);
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation("Switched to session '{SessionName}' ({SessionId}) on server {ServerId}",
            targetSession.Name, sessionId, serverId);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameSessionAsync(string sessionId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Session name cannot be empty", nameof(newName));

        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Attempted to rename non-existent session {SessionId}", sessionId);
            return false;
        }

        // Check if a session with this name already exists on the same server
        var existingSession = await _sessionRepository.AsQueryable()
            .FirstOrDefaultAsync(s => s.Scope == SessionScope.Server &&
                                     s.OwningEntityId == session.OwningEntityId &&
                                     s.Name == newName &&
                                     s.Id != sessionId);

        if (existingSession != null)
            throw new InvalidOperationException($"A session with the name '{newName}' already exists");

        var oldName = session.Name;
        session.Name = newName;
        session.LastActivityAt = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation("Renamed session {SessionId} from '{OldName}' to '{NewName}'", sessionId, oldName, newName);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSessionAsync(ulong serverId, string sessionId)
    {
        var session = await _sessionRepository.AsQueryable()
            .FirstOrDefaultAsync(s => s.Id == sessionId &&
                                     s.Scope == SessionScope.Server &&
                                     s.OwningEntityId == serverId);

        if (session == null)
        {
            _logger.LogWarning("Attempted to delete non-existent session {SessionId} on server {ServerId}", sessionId, serverId);
            return false;
        }

        // Check if this is the last session for the server
        var serverSessionCount = await _sessionRepository.AsQueryable()
            .CountAsync(s => s.Scope == SessionScope.Server && s.OwningEntityId == serverId);

        if (serverSessionCount <= 1)
        {
            _logger.LogWarning("Cannot delete the last session {SessionId} on server {ServerId}", sessionId, serverId);
            return false; // Cannot delete the last session
        }

        // If this was the active session, activate another one
        if (session.IsActive)
        {
            var otherSession = await _sessionRepository.AsQueryable()
                .Where(s => s.Scope == SessionScope.Server &&
                           s.OwningEntityId == serverId &&
                           s.Id != sessionId)
                .OrderByDescending(s => s.LastActivityAt)
                .FirstOrDefaultAsync();

            if (otherSession != null)
            {
                otherSession.IsActive = true;
                await _sessionRepository.UpdateAsync(otherSession);

                // Clear the message cache since we're switching to a different session
                _messageCacheService.ClearServerMessageCache(serverId);
            }
        }

        await _sessionRepository.RemoveAsync(session);
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation("Deleted session '{SessionName}' ({SessionId}) on server {ServerId}",
            session.Name, sessionId, serverId);

        return true;
    }

    /// <inheritdoc/>
    public async Task<SessionStats?> GetSessionStatsAsync(string sessionId)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
            return null;

        return new SessionStats
        {
            SessionId = session.Id,
            Name = session.Name,
            CreatedAt = session.CreatedAt,
            LastActivityAt = session.LastActivityAt,
            MessageCount = session.MessageCount,
            EstimatedTokens = session.EstimatedTokens,
            Model = session.Model,
            Provider = session.Provider,
            IsActive = session.IsActive
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ArchiveSessionAsync(string sessionId)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Attempted to archive non-existent session {SessionId}", sessionId);
            return false;
        }

        // If this was the active session, we need to activate another one
        if (session.IsActive)
        {
            var otherSession = await _sessionRepository.AsQueryable()
                .Where(s => s.Scope == SessionScope.Server &&
                           s.OwningEntityId == session.OwningEntityId &&
                           s.Id != sessionId)
                .OrderByDescending(s => s.LastActivityAt)
                .FirstOrDefaultAsync();

            if (otherSession != null)
            {
                otherSession.IsActive = true;
                await _sessionRepository.UpdateAsync(otherSession);

                // Clear the message cache since we're switching to a different session
                _messageCacheService.ClearServerMessageCache(session.OwningEntityId);
            }
        }

        session.IsActive = false;
        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation("Archived session '{SessionName}' ({SessionId})", session.Name, sessionId);

        return true;
    }

    private async Task DeactivateAllSessionsAsync(ulong serverId)
    {
        var activeSessions = await _sessionRepository.AsQueryable()
            .Where(s => s.Scope == SessionScope.Server &&
                       s.OwningEntityId == serverId &&
                       s.IsActive)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.IsActive = false;
            await _sessionRepository.UpdateAsync(session);
        }

        if (activeSessions.Any())
        {
            await _sessionRepository.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsSessionStaleAsync(string sessionId, int inactivityTimeoutMinutes = 30)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
            return false;

        var inactivityDuration = DateTime.UtcNow - session.LastActivityAt;
        return inactivityDuration.TotalMinutes > inactivityTimeoutMinutes;
    }

    /// <inheritdoc/>
    public async Task<SessionRefreshResult> RefreshStaleSessionAsync(ulong serverId, ulong userId)
    {
        var currentSession = await GetActiveSessionAsync(serverId);
        var sessionOptions = _memoryOptions.Session;

        if (currentSession == null)
        {
            // No active session - create a new one
            var newSession = await CreateSessionAsync(serverId, $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}", true);
            return new SessionRefreshResult
            {
                WasRefreshed = true,
                SessionId = newSession.Id,
                InactivityDuration = TimeSpan.Zero
            };
        }

        var inactivityDuration = DateTime.UtcNow - currentSession.LastActivityAt;
        var isStale = inactivityDuration.TotalMinutes > sessionOptions.InactivityTimeoutMinutes;

        if (!isStale)
        {
            // Session is still fresh
            return new SessionRefreshResult
            {
                WasRefreshed = false,
                SessionId = currentSession.Id,
                InactivityDuration = inactivityDuration
            };
        }

        _logger.LogInformation(
            "Refreshing stale session {SessionId} for server {ServerId}. Inactive for {Minutes} minutes",
            currentSession.Id, serverId, (int)inactivityDuration.TotalMinutes);

        // 1. Extract memories from the old session before clearing
        string? previousSummary = null;
        int memoriesCreated = 0;

        if (_memoryService != null && sessionOptions.CreateSummaryOnRefresh)
        {
            try
            {
                // Get session messages for memory extraction
                var messages = await _sessionMessageRepository.GetSessionMessagesAsync(currentSession.Id, 50);
                if (messages.Count >= _memoryOptions.MinMessagesForMemory)
                {
                    var extractedMemories = await _memoryService.ExtractMemoriesFromConversationAsync(
                        currentSession.Id, messages);
                    memoriesCreated = extractedMemories.Count;

                    // Create a summary memory if there were messages
                    if (messages.Count > 0)
                    {
                        var summaryContent = $"Previous conversation ended {inactivityDuration.TotalMinutes:F0} minutes ago. " +
                                            $"Had {messages.Count} messages.";
                        previousSummary = summaryContent;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract memories during session refresh for {SessionId}", currentSession.Id);
            }
        }

        // 2. Clear the message cache and session messages (start fresh)
        _messageCacheService.ClearServerMessageCache(serverId);

        // 3. Delete old session messages to start fresh (keep 0 = delete all)
        await _sessionMessageRepository.TrimSessionMessagesAsync(currentSession.Id, 0);

        // 4. Reset session counters
        currentSession.MessageCount = 0;
        currentSession.EstimatedTokens = 0;
        currentSession.Context = null;
        currentSession.ContextTokens = 0;
        currentSession.LastActivityAt = DateTime.UtcNow;
        await _sessionRepository.UpdateAsync(currentSession);
        await _sessionRepository.SaveChangesAsync();

        // 5. Fetch relevant memories for the new session context
        string? memoryContext = null;
        int memoriesRetrieved = 0;

        if (_memoryService != null)
        {
            try
            {
                memoryContext = await _memoryService.GetCombinedMemoryContextAsync(
                    currentSession.Id, userId, serverId, null);

                if (!string.IsNullOrEmpty(memoryContext))
                {
                    memoriesRetrieved = sessionOptions.MaxMemoriesOnSessionRefresh;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve memories during session refresh for {SessionId}", currentSession.Id);
            }
        }

        _logger.LogInformation(
            "Session {SessionId} refreshed. Created {MemoriesCreated} memories, retrieved {MemoriesRetrieved} for context",
            currentSession.Id, memoriesCreated, memoriesRetrieved);

        return new SessionRefreshResult
        {
            WasRefreshed = true,
            SessionId = currentSession.Id,
            MemoryContext = memoryContext,
            MemoriesRetrieved = memoriesRetrieved,
            InactivityDuration = inactivityDuration,
            PreviousSessionSummary = previousSummary
        };
    }

    /// <inheritdoc/>
    public async Task<SessionCompactionResult> CompactSessionAsync(string sessionId, int messagesToKeep = 10)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
        {
            return new SessionCompactionResult { WasCompacted = false };
        }

        var messages = await _sessionMessageRepository.GetSessionMessagesAsync(sessionId, int.MaxValue);

        if (messages.Count <= messagesToKeep)
        {
            return new SessionCompactionResult
            {
                WasCompacted = false,
                MessagesKept = messages.Count
            };
        }

        _logger.LogInformation(
            "Compacting session {SessionId}: {Total} messages -> keeping {Keep}",
            sessionId, messages.Count, messagesToKeep);

        // Sort by creation time to get oldest first
        var orderedMessages = messages.OrderBy(m => m.CreatedAt).ToList();
        var messagesToArchive = orderedMessages.Take(messages.Count - messagesToKeep).ToList();
        var messagesToRetain = orderedMessages.Skip(messages.Count - messagesToKeep).ToList();

        int memoriesCreated = 0;
        string? conversationSummary = null;

        // Extract memories from messages to archive
        if (_memoryService != null && _memoryOptions.Session.ExtractMemoriesOnCompaction)
        {
            try
            {
                var extractedMemories = await _memoryService.ExtractMemoriesFromConversationAsync(
                    sessionId, messagesToArchive);
                memoriesCreated = extractedMemories.Count;

                // Create a simple summary
                conversationSummary = $"Compacted {messagesToArchive.Count} messages. Topics discussed included: " +
                    string.Join(", ", messagesToArchive
                        .Where(m => m.Role == "user")
                        .Take(3)
                        .Select(m => m.Content.Length > 50 ? m.Content[..50] + "..." : m.Content));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract memories during compaction for {SessionId}", sessionId);
            }
        }

        // Delete archived messages by keeping only the ones we want to retain
        // Since we want to keep messagesToKeep, we use TrimSessionMessagesAsync
        await _sessionMessageRepository.TrimSessionMessagesAsync(sessionId, messagesToKeep);

        // Update session counters
        session.MessageCount = messagesToRetain.Count;
        session.EstimatedTokens = messagesToRetain.Sum(m => m.EstimatedTokens);
        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        // Clear message cache to force reload
        _messageCacheService.ClearServerMessageCache(session.OwningEntityId);

        _logger.LogInformation(
            "Session {SessionId} compacted. Archived {Archived} messages, created {Memories} memories",
            sessionId, messagesToArchive.Count, memoriesCreated);

        return new SessionCompactionResult
        {
            WasCompacted = true,
            MessagesArchived = messagesToArchive.Count,
            MessagesKept = messagesToRetain.Count,
            MemoriesCreated = memoriesCreated,
            ConversationSummary = conversationSummary
        };
    }

    /// <inheritdoc/>
    public async Task UpdateSessionActivityAsync(string sessionId)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session != null)
        {
            session.LastActivityAt = DateTime.UtcNow;
            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> NeedsCompactionAsync(string sessionId, int maxMessages = 50)
    {
        var session = await _sessionRepository.GetAsync(sessionId);
        if (session == null)
            return false;

        return session.MessageCount > maxMessages;
    }
}