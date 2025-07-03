namespace Amiquin.Core.Services.Chat.Toggle;

/// <summary>
/// Service interface for managing server toggle operations.
/// Provides methods for creating, retrieving, updating toggle states and configurations.
/// </summary>
public interface IToggleService
{
    /// <summary>
    /// Checks if a specific toggle is enabled for a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to check the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to check.</param>
    /// <returns>True if the toggle is enabled; otherwise, false.</returns>
    Task<bool> IsEnabledAsync(ulong serverId, string toggleName);

    /// <summary>
    /// Creates default toggles for a server if they don't already exist.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create toggles for.</param>
    Task CreateServerTogglesIfNotExistsAsync(ulong serverId);

    /// <summary>
    /// Retrieves all toggles for a specific server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve toggles for.</param>
    /// <returns>A list of toggles for the specified server.</returns>
    Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId);

    /// <summary>
    /// Sets the state of a specific toggle for a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to set the toggle for.</param>
    /// <param name="toggleName">The name of the toggle to set.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled or disabled.</param>
    /// <param name="description">Optional description for the toggle.</param>
    Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null);

    /// <summary>
    /// Sets multiple toggles for a server in a single operation.
    /// </summary>
    /// <param name="serverId">The Discord server ID to set toggles for.</param>
    /// <param name="toggles">Dictionary of toggle names with their enabled state and description.</param>
    Task SetServerTogglesBulkAsync(ulong serverId, Dictionary<string, (bool IsEnabled, string? Description)> toggles);

    /// <summary>
    /// Updates a specific toggle across all servers in the system.
    /// </summary>
    /// <param name="toggleName">The name of the toggle to update.</param>
    /// <param name="isEnabled">Whether the toggle should be enabled or disabled.</param>
    /// <param name="description">Optional description for the toggle.</param>
    /// <returns>True if the operation was successful; otherwise, false.</returns>
    Task<bool> UpdateAllTogglesAsync(string toggleName, bool isEnabled, string? description = null);
}
