namespace Amiquin.Core.Services.Sleep;

/// <summary>
/// Service for managing bot sleep functionality per server.
/// </summary>
public interface ISleepService
{
    /// <summary>
    /// Puts the bot to sleep for the specified duration on a server.
    /// </summary>
    /// <param name="serverId">Server ID to put to sleep.</param>
    /// <param name="durationMinutes">Duration in minutes to sleep.</param>
    /// <returns>DateTime when the bot will wake up.</returns>
    Task<DateTime> PutToSleepAsync(ulong serverId, int durationMinutes);

    /// <summary>
    /// Checks if the bot is currently sleeping on a server.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>True if sleeping, false if awake.</returns>
    Task<bool> IsSleepingAsync(ulong serverId);

    /// <summary>
    /// Gets the remaining sleep time for a server.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>Remaining sleep duration, or null if not sleeping.</returns>
    Task<TimeSpan?> GetRemainingSleepTimeAsync(ulong serverId);

    /// <summary>
    /// Manually wakes up the bot on a server (admin override).
    /// </summary>
    /// <param name="serverId">Server ID to wake up.</param>
    /// <returns>True if bot was sleeping and is now awake, false if already awake.</returns>
    Task<bool> WakeUpAsync(ulong serverId);

    /// <summary>
    /// Gets all currently sleeping servers with their wake-up times.
    /// </summary>
    /// <returns>Dictionary of server IDs and their wake-up times.</returns>
    Task<Dictionary<ulong, DateTime>> GetSleepingServersAsync();
}