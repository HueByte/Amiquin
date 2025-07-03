namespace Amiquin.Core.Services.Nacho;

/// <summary>
/// Service interface for managing nacho operations.
/// Handles nacho counting, addition, removal, and statistics tracking for users and servers.
/// </summary>
public interface INachoService
{
    /// <summary>
    /// Adds nacho count to a user in a specific server.
    /// </summary>
    /// <param name="userId">The Discord user ID to add nachos for.</param>
    /// <param name="serverId">The Discord server ID where the nachos are being added.</param>
    /// <param name="nachoCount">The number of nachos to add. Default is 1.</param>
    Task AddNachoAsync(ulong userId, ulong serverId, int nachoCount = 1);

    /// <summary>
    /// Retrieves the total nacho count for a specific server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to get the nacho count for.</param>
    /// <returns>The total number of nachos in the server.</returns>
    Task<int> GetServerNachoCountAsync(ulong serverId);

    /// <summary>
    /// Retrieves the total nacho count across all servers.
    /// </summary>
    /// <returns>The total number of nachos across all servers.</returns>
    Task<int> GetTotalNachoCountAsync();

    /// <summary>
    /// Retrieves the total nacho count for a specific user across all servers.
    /// </summary>
    /// <param name="userId">The Discord user ID to get the nacho count for.</param>
    /// <returns>The total number of nachos for the user.</returns>
    Task<int> GetUserNachoCountAsync(ulong userId);

    /// <summary>
    /// Removes all nachos for a specific user across all servers.
    /// </summary>
    /// <param name="userId">The Discord user ID to remove nachos for.</param>
    Task RemoveAllNachoAsync(ulong userId);

    /// <summary>
    /// Removes all nachos for a specific server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to remove nachos for.</param>
    Task RemoveAllServerNachoAsync(ulong serverId);

    /// <summary>
    /// Removes nacho records for a specific user in a specific server.
    /// </summary>
    /// <param name="userId">The Discord user ID to remove nachos for.</param>
    /// <param name="serverId">The Discord server ID to remove nachos from.</param>
    Task RemoveNachoAsync(ulong userId, ulong serverId);
}
