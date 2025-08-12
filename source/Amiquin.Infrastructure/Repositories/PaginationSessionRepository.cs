using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing pagination sessions
/// </summary>
public class PaginationSessionRepository : IPaginationSessionRepository
{
    private readonly AmiquinContext _context;

    public PaginationSessionRepository(AmiquinContext context)
    {
        _context = context;
    }

    public async Task<PaginationSession> CreateAsync(PaginationSession session, CancellationToken cancellationToken = default)
    {
        _context.PaginationSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<PaginationSession?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _context.PaginationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.IsActive, cancellationToken);
    }

    public async Task<PaginationSession> UpdateAsync(PaginationSession session, CancellationToken cancellationToken = default)
    {
        _context.PaginationSessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _context.PaginationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
            return false;

        _context.PaginationSessions.Remove(session);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<PaginationSession>> GetActiveByUserAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        return await _context.PaginationSessions
            .Where(s => s.UserId == userId && s.IsActive && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PaginationSession>> GetExpiredAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PaginationSessions
            .Where(s => s.ExpiresAt <= DateTime.UtcNow || !s.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expiredSessions = await GetExpiredAsync(cancellationToken);

        if (!expiredSessions.Any())
            return 0;

        _context.PaginationSessions.RemoveRange(expiredSessions);
        await _context.SaveChangesAsync(cancellationToken);

        return expiredSessions.Count;
    }

    public async Task<PaginationSession?> GetByMessageIdAsync(ulong messageId, CancellationToken cancellationToken = default)
    {
        return await _context.PaginationSessions
            .FirstOrDefaultAsync(s => s.MessageId == messageId && s.IsActive && s.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }
}