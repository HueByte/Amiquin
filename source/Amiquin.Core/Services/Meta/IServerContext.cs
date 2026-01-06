namespace Amiquin.Core.Services.Meta;

/// <summary>
/// Scoped service that holds server context data for the lifetime of a request/scope.
/// Provides lazy loading and caching of ServerMeta for the current request.
/// </summary>
public interface IServerContext
{
    /// <summary>
    /// Gets or sets the current server ID for this scope.
    /// </summary>
    ulong? ServerId { get; }

    /// <summary>
    /// Gets or sets the current server name for this scope.
    /// </summary>
    string? ServerName { get; }

    /// <summary>
    /// Gets whether the context has been initialized with a server.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the ServerMeta for the current scope. Returns null if not initialized.
    /// </summary>
    Models.ServerMeta? ServerMeta { get; }

    /// <summary>
    /// Initializes the server context with the specified server ID and name.
    /// This will trigger lazy loading of ServerMeta on first access.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="serverName">The Discord server name.</param>
    void Initialize(ulong serverId, string? serverName = null);

    /// <summary>
    /// Gets or creates the ServerMeta for the current scope.
    /// If the context is not initialized, throws InvalidOperationException.
    /// </summary>
    /// <returns>The ServerMeta for the current server.</returns>
    /// <exception cref="InvalidOperationException">Thrown when context is not initialized.</exception>
    Task<Models.ServerMeta> GetOrCreateServerMetaAsync();

    /// <summary>
    /// Refreshes the cached ServerMeta from the database.
    /// Useful after updates to ensure fresh data.
    /// </summary>
    Task RefreshAsync();
}
