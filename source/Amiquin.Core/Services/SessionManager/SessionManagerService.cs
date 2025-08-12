using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.MessageCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChatSessionModel = Amiquin.Core.Models.ChatSession;

namespace Amiquin.Core.Services.SessionManager;

/// <summary>
/// Service implementation for managing multiple chat sessions per server.
/// </summary>
public class SessionManagerService : ISessionManagerService
{
    private readonly IChatSessionRepository _sessionRepository;
    private readonly IMessageCacheService _messageCacheService;
    private readonly ILogger<SessionManagerService> _logger;

    public SessionManagerService(
        IChatSessionRepository sessionRepository,
        IMessageCacheService messageCacheService,
        ILogger<SessionManagerService> logger)
    {
        _sessionRepository = sessionRepository;
        _messageCacheService = messageCacheService;
        _logger = logger;
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
}