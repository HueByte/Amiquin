using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.ServerMeta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat.Toggle;

public class ToggleService : IToggleService
{
    private readonly ILogger<ToggleService> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleRepository _toggleRepository;
    private readonly IMemoryCache _memoryCache;

    public ToggleService(ILogger<ToggleService> logger, IServerMetaService serverMetaService, IToggleRepository toggleRepository, IMemoryCache memoryCache)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleRepository = toggleRepository;
        _memoryCache = memoryCache;
    }

    public async Task<List<Models.Toggle>> GetOrCreateGlobalTogglesAsync()
    {
        var expectedToggles = Constants.ToggleNames.Toggles;
        if (_memoryCache.TryGetValue(Constants.CacheKeys.GlobalToggles, out List<Models.Toggle>? cachedToggles)
            && cachedToggles is not null)
        {
            _logger.LogInformation("Returning cached global toggles");
            return cachedToggles;
        }

        var globalToggles = await _toggleRepository.AsQueryable()
            .Where(x => x.Scope == Models.ToggleScope.Global)
            .ToListAsync();

        if (globalToggles is not null && globalToggles.Any())
        {
            var missingToggles = expectedToggles.Except(globalToggles.Select(t => t.Name)).ToList();
            if (missingToggles.Any())
            {
                var missingTogglesModels = missingToggles.Select(toggleName => new Models.Toggle
                {
                    Name = toggleName,
                    IsEnabled = true,
                    Description = string.Empty,
                    Scope = Models.ToggleScope.Global,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _toggleRepository.AddRangeAsync(missingTogglesModels);

                _logger.LogInformation("Added missing global toggles: {toggles}", string.Join(", ", missingToggles));

                await _toggleRepository.SaveChangesAsync();

                globalToggles.AddRange(missingTogglesModels);
            }
        }

        _memoryCache.Set(Constants.CacheKeys.GlobalToggles, globalToggles,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Cache for 1 hour
            });

        return globalToggles ?? [];
    }

    public async Task SetGlobalToggleAsync(string toggleName, bool isEnabled, string? description = null)
    {
        var globalToggles = await GetOrCreateGlobalTogglesAsync();
        var toggle = globalToggles.FirstOrDefault(x => x.Name == toggleName);

        if (toggle is not null)
        {
            toggle.IsEnabled = isEnabled;
            toggle.Description = description ?? toggle.Description;
        }
        else
        {
            toggle = new Models.Toggle
            {
                Name = toggleName,
                IsEnabled = isEnabled,
                Description = description ?? string.Empty,
                Scope = Models.ToggleScope.Global,
                CreatedAt = DateTime.UtcNow
            };

            globalToggles.Add(toggle);
        }

        await _toggleRepository.UpdateAsync(toggle);
        await _toggleRepository.SaveChangesAsync();

        _memoryCache.Set(Constants.CacheKeys.GlobalToggles, globalToggles,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Cache for 1 hour
            });

        _logger.LogInformation("Set global toggle {toggleName} to {isEnabled}", toggleName, isEnabled);
    }

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

            _logger.LogInformation("Server toggles already exist for serverId {serverId}", serverId);
            return;
        }

        var serverToggles = serverMeta.Toggles ?? [];
        await SetServerTogglesBulkAsync(serverId, expectedToggles.ToDictionary(x => x, x => (true, (string?)string.Empty)));
    }

    /// <summary>
    /// Checks if toggle is enabled based on the hierarchy<br>
    /// System -> Server
    /// </summary>
    /// <param name="toggleName"></param>
    /// <param name="serverId"></param>
    /// <returns></returns>
    public async Task<bool> IsEnabledAsync(ulong serverId, string toggleName)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
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

    public async Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {serverId}", serverId);
            return;
        }

        var toggle = serverMeta.Toggles?.FirstOrDefault(x => x.Name == toggleName);
        if (toggle is not null)
        {
            toggle.IsEnabled = isEnabled;
            toggle.Description = description ?? toggle.Description;
        }
        else
        {
            toggle = new Models.Toggle
            {
                ServerId = serverId,
                Name = toggleName,
                IsEnabled = isEnabled,
                Description = description ?? string.Empty,
                Scope = Models.ToggleScope.Server,
                CreatedAt = DateTime.UtcNow
            };

            serverMeta.Toggles ??= [];
            serverMeta.Toggles.Add(toggle);
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
    }

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
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                    Scope = Models.ToggleScope.Server
                };

                serverMeta.Toggles.Add(toggle);
            }
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
    }

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
                toggle.IsEnabled = isEnabled;
                toggle.Description = description ?? toggle.Description;
            }
            else
            {
                toggle = new Models.Toggle
                {
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                    Scope = Models.ToggleScope.Global // Assuming global scope for all servers
                };

                serverMeta.Toggles ??= new List<Models.Toggle>();
                serverMeta.Toggles.Add(toggle);
            }

            serverMeta.LastUpdated = DateTime.UtcNow;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);
        }

        _logger.LogInformation("Updated toggle {toggleName} for all servers", toggleName);
        return true;
    }
}