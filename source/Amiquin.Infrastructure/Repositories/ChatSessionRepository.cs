using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing chat sessions
/// </summary>
public class ChatSessionRepository : QueryableBaseRepository<string, ChatSession, AmiquinContext>, IChatSessionRepository
{
    public ChatSessionRepository(AmiquinContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<ChatSession?> GetActiveSessionAsync(SessionScope scope, ulong userId = 0, ulong channelId = 0, ulong serverId = 0)
    {
        var owningEntityId = ChatSession.GetOwningEntityId(scope, userId, channelId, serverId);

        return await _context.ChatSessions
            .Where(s => s.Scope == scope && s.OwningEntityId == owningEntityId && s.IsActive)
            .OrderByDescending(s => s.LastActivityAt)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc/>
    public async Task<ChatSession> GetOrCreateActiveSessionAsync(SessionScope scope, ulong userId = 0, ulong channelId = 0, ulong serverId = 0, string model = "gpt-4o-mini", string provider = "OpenAI")
    {
        var existingSession = await GetActiveSessionAsync(scope, userId, channelId, serverId);
        if (existingSession != null)
        {
            // Update last activity time
            existingSession.LastActivityAt = DateTime.UtcNow;
            await SaveChangesAsync();
            return existingSession;
        }

        // Create new session
        var newSession = ChatSession.CreateSession(scope, userId, channelId, serverId, model, provider);
        await AddAsync(newSession);
        await SaveChangesAsync();

        return newSession;
    }

    /// <inheritdoc/>
    public async Task<ChatSession?> UpdateSessionModelAsync(string sessionId, string model, string provider)
    {
        var session = await GetAsync(sessionId);
        if (session == null)
            return null;

        session.Model = model;
        session.Provider = provider;
        session.LastActivityAt = DateTime.UtcNow;

        await SaveChangesAsync();
        return session;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateSessionModelByScopeAsync(SessionScope scope, string model, string provider, ulong userId = 0, ulong channelId = 0, ulong serverId = 0)
    {
        var owningEntityId = ChatSession.GetOwningEntityId(scope, userId, channelId, serverId);

        var sessions = await _context.ChatSessions
            .Where(s => s.Scope == scope && s.OwningEntityId == owningEntityId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.Model = model;
            session.Provider = provider;
            session.LastActivityAt = DateTime.UtcNow;
        }

        if (sessions.Count > 0)
        {
            await SaveChangesAsync();
        }

        return sessions.Count;
    }

    /// <inheritdoc/>
    public async Task<int> DeactivateOldSessionsAsync(SessionScope scope, ulong owningEntityId, int keepCount = 5)
    {
        var sessionsToDeactivate = await _context.ChatSessions
            .Where(s => s.Scope == scope && s.OwningEntityId == owningEntityId && s.IsActive)
            .OrderByDescending(s => s.LastActivityAt)
            .Skip(keepCount)
            .ToListAsync();

        foreach (var session in sessionsToDeactivate)
        {
            session.IsActive = false;
        }

        if (sessionsToDeactivate.Count > 0)
        {
            await SaveChangesAsync();
        }

        return sessionsToDeactivate.Count;
    }

    /// <inheritdoc/>
    public async Task<ChatSession?> UpdateSessionContextAsync(string sessionId, string context, int contextTokens)
    {
        var session = await GetAsync(sessionId);
        if (session == null)
            return null;

        session.Context = context;
        session.ContextTokens = contextTokens;
        session.LastActivityAt = DateTime.UtcNow;

        await SaveChangesAsync();
        return session;
    }
}