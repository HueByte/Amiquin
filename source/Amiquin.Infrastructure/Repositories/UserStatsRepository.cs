using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using InfraUserStats = Amiquin.Infrastructure.Entities.UserStats;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for managing user statistics.
/// </summary>
public class UserStatsRepository : IUserStatsRepository
{
    private readonly AmiquinContext _context;
    private readonly ILogger<UserStatsRepository> _logger;

    public UserStatsRepository(AmiquinContext context, ILogger<UserStatsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserStats> GetOrCreateUserStatsAsync(ulong userId, ulong serverId)
    {
        var userStats = await _context.UserStats
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ServerId == serverId);

        if (userStats == null)
        {
            userStats = new InfraUserStats
            {
                UserId = userId,
                ServerId = serverId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserStats.Add(userStats);
            await _context.SaveChangesAsync();
            _logger.LogDebug("Created new user stats record for user {UserId} in server {ServerId}", userId, serverId);
        }

        return userStats;
    }

    /// <inheritdoc/>
    public async Task UpdateUserStatsAsync(UserStats userStats)
    {
        if (userStats is InfraUserStats infraUserStats)
        {
            infraUserStats.UpdatedAt = DateTime.UtcNow;
            _context.UserStats.Update(infraUserStats);
            await _context.SaveChangesAsync();
            _logger.LogDebug("Updated user stats for user {UserId} in server {ServerId}", userStats.UserId, userStats.ServerId);
        }
        else
        {
            throw new ArgumentException("UserStats must be of type Infrastructure.Entities.UserStats");
        }
    }

    /// <inheritdoc/>
    public async Task<List<UserStats>> GetTopNachoGiversAsync(ulong serverId, int limit = 10)
    {
        var allStats = await _context.UserStats
            .Where(u => u.ServerId == serverId)
            .ToListAsync();

        // Filter and sort by nachos_given stat in memory since we can't query JSON in all databases the same way
        return allStats
            .Where(u => u.GetStat<int>("nachos_given", 0) > 0)
            .OrderByDescending(u => u.GetStat<int>("nachos_given", 0))
            .Take(limit)
            .Cast<UserStats>()
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalNachosReceivedAsync(ulong serverId)
    {
        var allStats = await _context.UserStats
            .Where(u => u.ServerId == serverId)
            .ToListAsync();

        // Sum nachos_given stat in memory
        return allStats.Sum(u => u.GetStat<int>("nachos_given", 0));
    }
}