using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for managing user statistics.
/// </summary>
public interface IUserStatsRepository
{
    /// <summary>
    /// Gets user stats for a specific user in a server, creating if it doesn't exist.
    /// </summary>
    /// <param name="userId">The Discord user ID.</param>
    /// <param name="serverId">The Discord server ID.</param>
    /// <returns>The user stats record.</returns>
    Task<UserStats> GetOrCreateUserStatsAsync(ulong userId, ulong serverId);

    /// <summary>
    /// Updates user stats in the database.
    /// </summary>
    /// <param name="userStats">The user stats to update.</param>
    /// <returns>A task representing the async operation.</returns>
    Task UpdateUserStatsAsync(UserStats userStats);

    /// <summary>
    /// Gets the top nacho givers for a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="limit">Maximum number of users to return (default 10).</param>
    /// <returns>List of user stats ordered by nachos given.</returns>
    Task<List<UserStats>> GetTopNachoGiversAsync(ulong serverId, int limit = 10);

    /// <summary>
    /// Gets total nachos received by Amiquin on a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <returns>Total nachos received.</returns>
    Task<int> GetTotalNachosReceivedAsync(ulong serverId);
}