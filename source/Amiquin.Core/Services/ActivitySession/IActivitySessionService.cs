namespace Amiquin.Core.Services.ActivitySession;

/// <summary>
/// Service for executing activity session logic for Discord servers
/// </summary>
public interface IActivitySessionService
{
    /// <summary>
    /// Executes activity session logic for a specific guild
    /// </summary>
    /// <param name="guildId">The guild ID to execute activity session for</param>
    /// <param name="adjustFrequencyCallback">Optional callback to adjust job frequency based on activity level</param>
    /// <returns>True if engagement action was executed, false otherwise</returns>
    Task<bool> ExecuteActivitySessionAsync(ulong guildId, Action<double>? adjustFrequencyCallback = null);
}