namespace Amiquin.Core.Services.Toggle;

/// <summary>
/// Service interface for managing server-scoped toggle operations.
/// All toggles are server-specific with no global override system.
/// </summary>
public interface IToggleService
{
    // ========== Toggle State Operations ==========

    /// <summary>
    /// Checks if a specific toggle is enabled for a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to check the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to check.</param>
    /// <returns>True if the toggle is enabled; otherwise, false.</returns>
    Task<bool> IsEnabledAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Gets toggle state information for a specific toggle.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="toggleName">The name of the toggle.</param>
    /// <returns>Toggle state with details.</returns>
    Task<ToggleState> GetToggleStateAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Gets all toggles for a server (includes all defined toggles, not just configured ones).
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <returns>List of all toggle states for the server.</returns>
    Task<List<ToggleState>> GetAllTogglesAsync(ulong serverId);

    // ========== Server Toggle Operations ==========

    /// <summary>
    /// Creates default toggles for a server if they don't already exist.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create toggles for.</param>
    Task CreateServerTogglesIfNotExistsAsync(ulong serverId);

    /// <summary>
    /// Sets a server toggle value.
    /// </summary>
    /// <param name="serverId">The Discord server ID to set the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to set.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled or disabled.</param>
    /// <param name="description">Optional description for the toggle.</param>
    Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null);

    /// <summary>
    /// Retrieves all toggles for a specific server from the database.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve toggles for.</param>
    /// <returns>A list of toggles for the specified server.</returns>
    Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId);

    /// <summary>
    /// Removes toggles that are no longer defined in the constants list for a specific server.
    /// This helps maintain database cleanliness by removing obsolete toggles.
    /// </summary>
    /// <param name="serverId">The Discord server ID to clean up toggles for.</param>
    /// <returns>The number of obsolete toggles that were removed.</returns>
    Task<int> RemoveObsoleteTogglesAsync(ulong serverId);
}

/// <summary>
/// Represents the state of a toggle.
/// </summary>
public class ToggleState
{
    /// <summary>
    /// The name of the toggle.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the toggle is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Description of the toggle.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for organizing toggles.
    /// </summary>
    public string Category { get; set; } = "General";
}
