using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing SessionMessage entities.
/// </summary>
public class SessionMessageRepository : BaseRepository<string, SessionMessage, AmiquinContext>, ISessionMessageRepository
{
    public SessionMessageRepository(AmiquinContext context) : base(context)
    {
    }

    /// <inheritdoc/>
    public async Task<List<SessionMessage>> GetSessionContextMessagesAsync(string chatSessionId, int limit = 50)
    {
        return await _context.SessionMessages
            .Where(x => x.ChatSessionId == chatSessionId && x.IncludeInContext)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .OrderBy(x => x.CreatedAt) // Re-order to oldest first for context
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<List<SessionMessage>> GetSessionMessagesAsync(string chatSessionId, int limit = 100)
    {
        return await _context.SessionMessages
            .Where(x => x.ChatSessionId == chatSessionId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .OrderBy(x => x.CreatedAt) // Re-order to oldest first
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<SessionMessage> AddSessionMessageAsync(
        string chatSessionId,
        string role,
        string content,
        string? discordMessageId = null,
        int estimatedTokens = 0,
        bool includeInContext = true)
    {
        var sessionMessage = new SessionMessage
        {
            Id = Guid.NewGuid().ToString(),
            ChatSessionId = chatSessionId,
            Role = role,
            Content = content,
            DiscordMessageId = discordMessageId,
            EstimatedTokens = estimatedTokens,
            IncludeInContext = includeInContext,
            CreatedAt = DateTime.UtcNow
        };

        await _context.SessionMessages.AddAsync(sessionMessage);
        await _context.SaveChangesAsync();

        return sessionMessage;
    }

    /// <inheritdoc/>
    public async Task<int> GetSessionMessageCountAsync(string chatSessionId)
    {
        return await _context.SessionMessages
            .Where(x => x.ChatSessionId == chatSessionId)
            .CountAsync();
    }

    /// <inheritdoc/>
    public async Task<int> TrimSessionMessagesAsync(string chatSessionId, int keepCount)
    {
        // Get messages to remove (all but the most recent keepCount messages)
        var messagesToRemove = await _context.SessionMessages
            .Where(x => x.ChatSessionId == chatSessionId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(keepCount)
            .ToListAsync();

        if (messagesToRemove.Count == 0)
            return 0;

        _context.SessionMessages.RemoveRange(messagesToRemove);
        await _context.SaveChangesAsync();

        return messagesToRemove.Count;
    }
}