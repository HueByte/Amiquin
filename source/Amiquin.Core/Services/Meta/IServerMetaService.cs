using Amiquin.Core.DiscordExtensions;

namespace Amiquin.Core.Services.Meta;

/// <summary>
/// Service interface for managing server metadata operations.
/// Provides methods for creating, retrieving, updating, and deleting server-specific configuration and state data.
/// </summary>
public interface IServerMetaService
{
    /// <summary>
    /// Retrieves server metadata for the specified server ID.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve metadata for.</param>
    /// <returns>The server metadata if found; otherwise, null.</returns>
    Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId);

    /// <summary>
    /// Retrieves server metadata for the specified server ID with optional toggle inclusion.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve metadata for.</param>
    /// <param name="includeToggles">Whether to include server toggles in the response. Default is false.</param>
    /// <returns>The server metadata if found; otherwise, null.</returns>
    Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId, bool includeToggles = false);

    /// <summary>
    /// Retrieves existing server metadata or creates new metadata if it doesn't exist.
    /// </summary>
    /// <param name="context">The Discord interaction context containing server information.</param>
    /// <returns>The existing or newly created server metadata.</returns>
    /// <exception cref="ArgumentException">Thrown when the server ID cannot be determined from the context.</exception>
    Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ExtendedShardedInteractionContext context);

    /// <summary>
    /// Permanently deletes server metadata and all associated data for the specified server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to delete metadata for.</param>
    /// <exception cref="Exception">Thrown when the server metadata is not found.</exception>
    Task DeleteServerMetaAsync(ulong serverId);

    /// <summary>
    /// Retrieves metadata for all servers in the database.
    /// </summary>
    /// <returns>A list of all server metadata records.</returns>
    Task<List<Models.ServerMeta>> GetAllServerMetasAsync();

    /// <summary>
    /// Updates existing server metadata with new values.
    /// </summary>
    /// <param name="serverMeta">The server metadata object containing updated values.</param>
    /// <exception cref="ArgumentException">Thrown when the server ID is not set.</exception>
    /// <exception cref="Exception">Thrown when the server metadata is not found in the database.</exception>
    Task UpdateServerMetaAsync(Models.ServerMeta serverMeta);

    /// <summary>
    /// Creates new server metadata for the specified server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create metadata for.</param>
    /// <param name="serverName">The name of the Discord server.</param>
    /// <returns>The newly created or existing server metadata.</returns>
    /// <exception cref="ArgumentException">Thrown when the server ID is zero.</exception>
    Task<Models.ServerMeta> CreateServerMetaAsync(ulong serverId, string serverName);

    /// <summary>
    /// Retrieves existing server metadata or creates new metadata if it doesn't exist.
    /// This is the preferred method for most use cases as it doesn't require an interaction context.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="serverName">Optional server name, used when creating new metadata.</param>
    /// <returns>The existing or newly created server metadata.</returns>
    /// <exception cref="ArgumentException">Thrown when the server ID is zero.</exception>
    Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ulong serverId, string? serverName = null);
}