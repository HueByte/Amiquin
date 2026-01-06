namespace Amiquin.Core.Services.Sleep;

/// <summary>
/// Service for managing bot sleep functionality per server.
/// Supports both manual sleep (user-triggered) and deep sleep (inactivity-triggered).
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
    /// Checks if the bot is currently sleeping on a server (either manual or deep sleep).
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

    /// <summary>
    /// Checks if the bot is in deep sleep mode for a server (due to inactivity).
    /// Deep sleep prevents all initiative-based actions.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>True if in deep sleep, false otherwise.</returns>
    Task<bool> IsInDeepSleepAsync(ulong serverId);

    /// <summary>
    /// Records activity for a server, potentially waking it from deep sleep.
    /// </summary>
    /// <param name="serverId">Server ID where activity occurred.</param>
    Task RecordActivityAsync(ulong serverId);

    /// <summary>
    /// Gets the last activity timestamp for a server.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>Last activity timestamp, or null if no activity recorded.</returns>
    Task<DateTime?> GetLastActivityAsync(ulong serverId);

    /// <summary>
    /// Gets the current initiative state for a server.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>Initiative state information.</returns>
    Task<InitiativeState> GetInitiativeStateAsync(ulong serverId);

    /// <summary>
    /// Records that an initiative action was taken.
    /// </summary>
    /// <param name="serverId">Server ID where initiative occurred.</param>
    Task RecordInitiativeActionAsync(ulong serverId);

    /// <summary>
    /// Gets the probability multiplier for initiative actions based on current state.
    /// </summary>
    /// <param name="serverId">Server ID to check.</param>
    /// <returns>Multiplier between 0.0 and 1.0.</returns>
    Task<float> GetInitiativeProbabilityMultiplierAsync(ulong serverId);
}

/// <summary>
/// Represents the current initiative state for a server.
/// </summary>
public class InitiativeState
{
    /// <summary>
    /// Whether the server is in deep sleep mode.
    /// </summary>
    public bool IsInDeepSleep { get; set; }

    /// <summary>
    /// Whether the server is in manual sleep mode.
    /// </summary>
    public bool IsInManualSleep { get; set; }

    /// <summary>
    /// Whether the server is in gradual wake-up period.
    /// </summary>
    public bool IsWakingUp { get; set; }

    /// <summary>
    /// Last recorded activity timestamp.
    /// </summary>
    public DateTime? LastActivity { get; set; }

    /// <summary>
    /// Last initiative action timestamp.
    /// </summary>
    public DateTime? LastInitiativeAction { get; set; }

    /// <summary>
    /// Number of consecutive initiative actions without user interaction.
    /// </summary>
    public int ConsecutiveInitiatives { get; set; }

    /// <summary>
    /// Number of messages received since deep sleep ended.
    /// </summary>
    public int MessagesSinceWakeUp { get; set; }

    /// <summary>
    /// Current probability multiplier for initiative actions.
    /// </summary>
    public float ProbabilityMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Time when deep sleep started, if applicable.
    /// </summary>
    public DateTime? DeepSleepStarted { get; set; }

    /// <summary>
    /// Time when wake-up period started, if applicable.
    /// </summary>
    public DateTime? WakeUpStarted { get; set; }
}
