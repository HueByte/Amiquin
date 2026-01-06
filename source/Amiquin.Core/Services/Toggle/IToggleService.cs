namespace Amiquin.Core.Services.Toggle;

/// <summary>
/// Service interface for managing toggle operations.
/// Supports both global toggles (system-wide defaults) and server-specific overrides.
/// </summary>
public interface IToggleService
{
    // ========== Effective State (Global + Server) ==========

    /// <summary>
    /// Checks if a specific toggle is enabled, considering both global defaults and server overrides.
    /// Priority: Server override > Global default.
    /// </summary>
    /// <param name="serverId">The Discord server ID to check the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to check.</param>
    /// <returns>True if the toggle is enabled; otherwise, false.</returns>
    Task<bool> IsEnabledAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Gets effective toggle state information including whether it's from global or server override.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="toggleName">The name of the toggle.</param>
    /// <returns>Toggle state with source information.</returns>
    Task<ToggleState> GetToggleStateAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Gets all effective toggles for a server (merged global + server overrides).
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <returns>List of effective toggle states for the server.</returns>
    Task<List<ToggleState>> GetEffectiveTogglesAsync(ulong serverId);

    // ========== Global Toggle Operations ==========

    /// <summary>
    /// Gets the global default state for a toggle.
    /// </summary>
    /// <param name="toggleName">The name of the toggle.</param>
    /// <returns>True if globally enabled; otherwise, false.</returns>
    Task<bool> IsGloballyEnabledAsync(string toggleName);

    /// <summary>
    /// Sets the global default state for a toggle.
    /// </summary>
    /// <param name="toggleName">The name of the toggle.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled globally.</param>
    /// <param name="description">Optional description for the toggle.</param>
    /// <param name="allowServerOverride">Whether servers can override this global setting.</param>
    /// <param name="category">Category for organizing toggles (e.g., "Chat", "Voice").</param>
    Task SetGlobalToggleAsync(string toggleName, bool isEnabled, string? description = null, bool allowServerOverride = true, string? category = null);

    /// <summary>
    /// Gets all global toggles.
    /// </summary>
    /// <returns>List of all global toggles.</returns>
    Task<List<Models.GlobalToggle>> GetAllGlobalTogglesAsync();

    /// <summary>
    /// Gets a specific global toggle by name.
    /// </summary>
    /// <param name="toggleName">The name of the toggle.</param>
    /// <returns>The global toggle if found; otherwise, null.</returns>
    Task<Models.GlobalToggle?> GetGlobalToggleAsync(string toggleName);

    /// <summary>
    /// Ensures all expected global toggles exist with default values.
    /// Called during application startup to seed missing toggles.
    /// </summary>
    Task EnsureGlobalTogglesExistAsync();

    // ========== Server Toggle Operations ==========

    /// <summary>
    /// Creates default toggles for a server if they don't already exist.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create toggles for.</param>
    Task CreateServerTogglesIfNotExistsAsync(ulong serverId);

    /// <summary>
    /// Retrieves all server-specific toggle overrides.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve toggles for.</param>
    /// <returns>A list of server toggle overrides.</returns>
    Task<List<Models.Toggle>> GetServerToggleOverridesAsync(ulong serverId);

    /// <summary>
    /// Sets a server-specific toggle override.
    /// </summary>
    /// <param name="serverId">The Discord server ID to set the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to set.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled or disabled.</param>
    /// <param name="description">Optional description for the toggle.</param>
    Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null);

    /// <summary>
    /// Sets multiple server-specific toggle overrides in a single operation.
    /// </summary>
    /// <param name="serverId">The Discord server ID to set toggles for.</param>
    /// <param name="toggles">Dictionary of toggle names with their enabled state and description.</param>
    Task SetServerTogglesBulkAsync(ulong serverId, Dictionary<string, (bool IsEnabled, string? Description)> toggles);

    /// <summary>
    /// Resets a server toggle to use the global default (removes server override).
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="toggleName">The name of the toggle to reset.</param>
    Task ResetServerToggleAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Resets all server toggles to use global defaults (removes all server overrides).
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    Task ResetAllServerTogglesAsync(ulong serverId);

    // ========== Legacy/Utility Methods ==========

    /// <summary>
    /// Retrieves all toggles for a specific server (server overrides, for backward compatibility).
    /// Prefer GetEffectiveTogglesAsync for merged view.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve toggles for.</param>
    /// <returns>A list of toggles for the specified server.</returns>
    Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId);

    /// <summary>
    /// Updates a specific toggle across all servers in the system.
    /// This sets server-specific overrides, not the global default.
    /// </summary>
    /// <param name="toggleName">The name of the toggle to update.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled or disabled.</param>
    /// <param name="description">Optional description for the toggle.</param>
    /// <returns>True if the operation was successful; otherwise, false.</returns>
    Task<bool> UpdateAllTogglesAsync(string toggleName, bool isEnabled, string? description = null);

    /// <summary>
    /// Removes toggles that are no longer defined in the constants list for a specific server.
    /// This helps maintain database cleanliness by removing obsolete toggles.
    /// </summary>
    /// <param name="serverId">The Discord server ID to clean up toggles for.</param>
    /// <returns>The number of obsolete toggles that were removed.</returns>
    Task<int> RemoveObsoleteTogglesAsync(ulong serverId);
}

/// <summary>
/// Represents the effective state of a toggle including its source.
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

    /// <summary>
    /// The source of this toggle state.
    /// </summary>
    public ToggleSource Source { get; set; }

    /// <summary>
    /// Whether this toggle can be overridden at server level.
    /// </summary>
    public bool AllowServerOverride { get; set; } = true;

    /// <summary>
    /// The global default value (for comparison when server override exists).
    /// </summary>
    public bool? GlobalDefault { get; set; }
}

/// <summary>
/// Indicates the source of a toggle value.
/// </summary>
public enum ToggleSource
{
    /// <summary>
    /// Value comes from global default settings.
    /// </summary>
    Global,

    /// <summary>
    /// Value comes from server-specific override.
    /// </summary>
    ServerOverride,

    /// <summary>
    /// Toggle doesn't exist (using fallback).
    /// </summary>
    NotConfigured
}