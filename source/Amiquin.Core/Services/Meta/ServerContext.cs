using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Meta;

/// <summary>
/// Scoped implementation that holds server context data for the lifetime of a request/scope.
/// Provides lazy loading and caching of ServerMeta within the current request.
/// </summary>
public class ServerContext : IServerContext
{
    private readonly IServerMetaService _serverMetaService;
    private readonly ILogger<ServerContext> _logger;
    private Models.ServerMeta? _serverMeta;
    private bool _isLoaded;

    public ulong? ServerId { get; private set; }
    public string? ServerName { get; private set; }
    public bool IsInitialized => ServerId.HasValue && ServerId.Value != 0;
    public Models.ServerMeta? ServerMeta => _serverMeta;

    public ServerContext(
        IServerMetaService serverMetaService,
        ILogger<ServerContext> logger)
    {
        _serverMetaService = serverMetaService;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Initialize(ulong serverId, string? serverName = null)
    {
        if (serverId == 0)
        {
            throw new ArgumentException("Server ID cannot be zero.", nameof(serverId));
        }

        ServerId = serverId;
        ServerName = serverName;
        _isLoaded = false;
        _serverMeta = null;

        _logger.LogDebug("ServerContext initialized for server {ServerId} ({ServerName})",
            serverId, serverName ?? "unknown");
    }

    /// <inheritdoc />
    public async Task<Models.ServerMeta> GetOrCreateServerMetaAsync()
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException(
                "ServerContext is not initialized. Call Initialize() first or ensure the request has a valid server context.");
        }

        if (_isLoaded && _serverMeta is not null)
        {
            return _serverMeta;
        }

        _serverMeta = await _serverMetaService.GetOrCreateServerMetaAsync(ServerId!.Value, ServerName);
        _isLoaded = true;

        _logger.LogDebug("ServerMeta loaded for server {ServerId}", ServerId);
        return _serverMeta;
    }

    /// <inheritdoc />
    public async Task RefreshAsync()
    {
        if (!IsInitialized)
        {
            _logger.LogWarning("Cannot refresh ServerContext - not initialized");
            return;
        }

        _isLoaded = false;
        _serverMeta = null;
        await GetOrCreateServerMetaAsync();
    }
}
