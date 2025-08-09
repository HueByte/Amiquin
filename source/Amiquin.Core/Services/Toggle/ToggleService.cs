using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.Meta;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Toggle;

/// <summary>
/// Service implementation for managing server toggle operations.
/// Handles toggle state management, default toggle creation, and server-specific toggle configurations.
/// </summary>
public class ToggleService : IToggleService
{
    private readonly ILogger<ToggleService> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleRepository _toggleRepository;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the ToggleService.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="serverMetaService">Service for managing server metadata operations.</param>
    /// <param name="toggleRepository">Repository for database operations on toggles.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed data.</param>
    public ToggleService(ILogger<ToggleService> logger, IServerMetaService serverMetaService, IToggleRepository toggleRepository, IMemoryCache memoryCache)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleRepository = toggleRepository;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc/>
    public async Task CreateServerTogglesIfNotExistsAsync(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
        var expectedToggles = Constants.ToggleNames.Toggles;
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return;
        }

        if (serverMeta.Toggles is not null && serverMeta.Toggles.Any())
        {
            var missingToggles = expectedToggles.Except(serverMeta.Toggles.Select(t => t.Name)).ToList();
            if (missingToggles.Any())
            {
                await SetServerTogglesBulkAsync(serverId, missingToggles.ToDictionary(x => x, x => (true, (string?)string.Empty)));
                _logger.LogInformation("Added missing toggles for serverId {serverId}", serverId);
            }

            return;
        }

        var serverToggles = serverMeta.Toggles ?? [];
        await SetServerTogglesBulkAsync(serverId, expectedToggles.ToDictionary(x => x, x => (true, (string?)string.Empty)));
    }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(ulong serverId, string toggleName)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, true);
        if (serverMeta is null)
        {
            _logger.LogWarning("Server meta not found for serverId {serverId}", serverId);
            return true;
        }

        if (!serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return false;
        }

        if (serverMeta.Toggles is not null && serverMeta.Toggles.Any(x => x.Name == toggleName))
        {
            var toggle = serverMeta.Toggles.First(x => x.Name == toggleName);
            return toggle.IsEnabled;
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, true);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return;
        }

        var toggle = serverMeta.Toggles?.FirstOrDefault(x => x.Name == toggleName);
        if (toggle is not null)
        {
            toggle.ServerId = serverId;
            toggle.IsEnabled = isEnabled;
            toggle.Description = description ?? toggle.Description;
        }
        else
        {
            toggle = new Models.Toggle
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Name = toggleName,
                IsEnabled = isEnabled,
                Description = description ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            serverMeta.Toggles ??= [];
            serverMeta.Toggles.Add(toggle);
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
    }

    /// <inheritdoc/>
    public async Task SetServerTogglesBulkAsync(ulong serverId, Dictionary<string, (bool IsEnabled, string? Description)> toggles)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return;
        }

        serverMeta.Toggles ??= new List<Models.Toggle>();
        foreach (var (toggleName, (isEnabled, description)) in toggles)
        {
            var toggle = serverMeta.Toggles.FirstOrDefault(x => x.Name == toggleName);
            if (toggle is not null)
            {
                toggle.IsEnabled = isEnabled;
                toggle.Description = description ?? toggle.Description;
            }
            else
            {
                toggle = new Models.Toggle
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                };

                serverMeta.Toggles.Add(toggle);
            }
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
    }

    /// <inheritdoc/>
    public async Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return [];
        }

        return serverMeta.Toggles ?? [];
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAllTogglesAsync(string toggleName, bool isEnabled, string? description = null)
    {
        var allServerMetas = await _serverMetaService.GetAllServerMetasAsync();
        if (allServerMetas.Count == 0)
        {
            _logger.LogWarning("No server metas found to update toggles");
            return false;
        }

        foreach (var serverMeta in allServerMetas)
        {
            var toggle = serverMeta.Toggles?.FirstOrDefault(x => x.Name == toggleName);
            if (toggle is not null)
            {
                toggle.ServerId = serverMeta.Id;
                toggle.IsEnabled = isEnabled;
                toggle.Description = description ?? toggle.Description;
            }
            else
            {
                toggle = new Models.Toggle
                {
                    Id = Guid.NewGuid().ToString(),
                    ServerId = serverMeta.Id,
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                };

                serverMeta.Toggles ??= [];
                serverMeta.Toggles.Add(toggle);
            }

            serverMeta.LastUpdated = DateTime.UtcNow;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);
        }

        _logger.LogInformation("Updated toggle {toggleName} for all servers", toggleName);
        return true;
    }
}