using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Utilities;
using Amiquin.Core.Utilities.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Toggle;

/// <summary>
/// Service implementation for managing toggle operations.
/// Supports global toggles (system-wide defaults) and server-specific overrides.
/// </summary>
public class ToggleService : IToggleService
{
    private readonly ILogger<ToggleService> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _memoryCache;

    private static readonly string GlobalTogglesCacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.GlobalToggles, "All");
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public ToggleService(
        ILogger<ToggleService> logger,
        IServerMetaService serverMetaService,
        IServiceProvider serviceProvider,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _serviceProvider = serviceProvider;
        _memoryCache = memoryCache;
    }

    // ========== Effective State (Global + Server) ==========

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(ulong serverId, string toggleName)
    {
        var state = await GetToggleStateAsync(serverId, toggleName);
        return state.IsEnabled;
    }

    /// <inheritdoc/>
    public async Task<ToggleState> GetToggleStateAsync(ulong serverId, string toggleName)
    {
        // Get global toggle first
        var globalToggle = await GetGlobalToggleAsync(toggleName);

        // Get server override
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        var serverToggle = serverMeta?.Toggles?.FirstOrDefault(t => t.Name == toggleName);

        // If global toggle doesn't allow override, use global value
        if (globalToggle is not null && !globalToggle.AllowServerOverride)
        {
            return new ToggleState
            {
                Name = toggleName,
                IsEnabled = globalToggle.IsEnabled,
                Description = globalToggle.Description,
                Category = globalToggle.Category,
                Source = ToggleSource.Global,
                AllowServerOverride = false,
                GlobalDefault = globalToggle.IsEnabled
            };
        }

        // Server override takes precedence
        if (serverToggle is not null)
        {
            return new ToggleState
            {
                Name = toggleName,
                IsEnabled = serverToggle.IsEnabled,
                Description = serverToggle.Description,
                Category = globalToggle?.Category ?? "General",
                Source = ToggleSource.ServerOverride,
                AllowServerOverride = globalToggle?.AllowServerOverride ?? true,
                GlobalDefault = globalToggle?.IsEnabled
            };
        }

        // Fall back to global toggle
        if (globalToggle is not null)
        {
            return new ToggleState
            {
                Name = toggleName,
                IsEnabled = globalToggle.IsEnabled,
                Description = globalToggle.Description,
                Category = globalToggle.Category,
                Source = ToggleSource.Global,
                AllowServerOverride = globalToggle.AllowServerOverride,
                GlobalDefault = globalToggle.IsEnabled
            };
        }

        // Toggle not configured anywhere
        _logger.LogDebug("Toggle {ToggleName} not found globally or for server {ServerId}", toggleName, serverId);
        return new ToggleState
        {
            Name = toggleName,
            IsEnabled = false,
            Description = string.Empty,
            Category = "General",
            Source = ToggleSource.NotConfigured,
            AllowServerOverride = true,
            GlobalDefault = null
        };
    }

    /// <inheritdoc/>
    public async Task<List<ToggleState>> GetEffectiveTogglesAsync(ulong serverId)
    {
        var result = new List<ToggleState>();

        // Get all global toggles
        var globalToggles = await GetAllGlobalTogglesAsync();
        var globalToggleDict = globalToggles.ToDictionary(t => t.Name, t => t);

        // Get server overrides
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        var serverToggleDict = serverMeta?.Toggles?.ToDictionary(t => t.Name, t => t) ?? new Dictionary<string, Models.Toggle>();

        // Merge all toggle names
        var allToggleNames = globalToggleDict.Keys.Union(serverToggleDict.Keys).Distinct();

        foreach (var toggleName in allToggleNames)
        {
            globalToggleDict.TryGetValue(toggleName, out var globalToggle);
            serverToggleDict.TryGetValue(toggleName, out var serverToggle);

            // Determine effective value
            bool isEnabled;
            ToggleSource source;

            if (globalToggle is not null && !globalToggle.AllowServerOverride)
            {
                isEnabled = globalToggle.IsEnabled;
                source = ToggleSource.Global;
            }
            else if (serverToggle is not null)
            {
                isEnabled = serverToggle.IsEnabled;
                source = ToggleSource.ServerOverride;
            }
            else if (globalToggle is not null)
            {
                isEnabled = globalToggle.IsEnabled;
                source = ToggleSource.Global;
            }
            else
            {
                isEnabled = false;
                source = ToggleSource.NotConfigured;
            }

            result.Add(new ToggleState
            {
                Name = toggleName,
                IsEnabled = isEnabled,
                Description = serverToggle?.Description ?? globalToggle?.Description ?? string.Empty,
                Category = globalToggle?.Category ?? "General",
                Source = source,
                AllowServerOverride = globalToggle?.AllowServerOverride ?? true,
                GlobalDefault = globalToggle?.IsEnabled
            });
        }

        return result.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
    }

    // ========== Global Toggle Operations ==========

    /// <inheritdoc/>
    public async Task<bool> IsGloballyEnabledAsync(string toggleName)
    {
        var globalToggle = await GetGlobalToggleAsync(toggleName);
        return globalToggle?.IsEnabled ?? false;
    }

    /// <inheritdoc/>
    public async Task SetGlobalToggleAsync(string toggleName, bool isEnabled, string? description = null, bool allowServerOverride = true, string? category = null)
    {
        await ExecuteWithGlobalToggleRepositoryAsync(async repo =>
        {
            var toggle = await repo.AsQueryable().FirstOrDefaultAsync(t => t.Name == toggleName);

            if (toggle is null)
            {
                toggle = new GlobalToggle
                {
                    Id = toggleName,
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                    AllowServerOverride = allowServerOverride,
                    Category = category ?? "General",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };
                await repo.AddAsync(toggle);
            }
            else
            {
                toggle.IsEnabled = isEnabled;
                toggle.Description = description ?? toggle.Description;
                toggle.AllowServerOverride = allowServerOverride;
                toggle.Category = category ?? toggle.Category;
                toggle.LastUpdatedAt = DateTime.UtcNow;
            }

            await repo.SaveChangesAsync();
        });

        // Invalidate cache
        _memoryCache.Remove(GlobalTogglesCacheKey);
        _logger.LogInformation("Global toggle {ToggleName} set to {IsEnabled}", toggleName, isEnabled);
    }

    /// <inheritdoc/>
    public async Task<List<GlobalToggle>> GetAllGlobalTogglesAsync()
    {
        if (_memoryCache.TryGetTypedValue(GlobalTogglesCacheKey, out List<GlobalToggle>? cached) && cached is not null)
        {
            return cached;
        }

        var toggles = await ExecuteWithGlobalToggleRepositoryAsync(async repo =>
            await repo.AsQueryable().OrderBy(t => t.Category).ThenBy(t => t.Name).ToListAsync());

        _memoryCache.SetAbsolute(GlobalTogglesCacheKey, toggles, CacheDuration);
        return toggles;
    }

    /// <inheritdoc/>
    public async Task<GlobalToggle?> GetGlobalToggleAsync(string toggleName)
    {
        var allToggles = await GetAllGlobalTogglesAsync();
        return allToggles.FirstOrDefault(t => t.Name == toggleName);
    }

    /// <inheritdoc/>
    public async Task EnsureGlobalTogglesExistAsync()
    {
        var expectedToggles = Constants.ToggleNames.Toggles;
        var existingToggles = await GetAllGlobalTogglesAsync();
        var existingNames = existingToggles.Select(t => t.Name).ToHashSet();

        var missingToggles = expectedToggles.Where(t => !existingNames.Contains(t)).ToList();

        if (missingToggles.Count == 0)
        {
            _logger.LogDebug("All expected global toggles already exist");
            return;
        }

        foreach (var toggleName in missingToggles)
        {
            await SetGlobalToggleAsync(
                toggleName,
                isEnabled: false, // Default to disabled
                description: GetDefaultToggleDescription(toggleName),
                allowServerOverride: true,
                category: GetToggleCategory(toggleName));
        }

        _logger.LogInformation("Created {Count} missing global toggles: {Toggles}",
            missingToggles.Count, string.Join(", ", missingToggles));
    }

    // ========== Server Toggle Operations ==========

    /// <inheritdoc/>
    public async Task CreateServerTogglesIfNotExistsAsync(ulong serverId)
    {
        // With the new global toggle system, server toggles are optional overrides.
        // We don't need to create toggles for every server anymore.
        // This method now just ensures the server meta exists.
        var serverMeta = await _serverMetaService.GetOrCreateServerMetaAsync(serverId);

        if (serverMeta.Toggles is null)
        {
            serverMeta.Toggles = [];
        }

        _logger.LogDebug("Server {ServerId} toggle infrastructure verified", serverId);
    }

    /// <inheritdoc/>
    public async Task<List<Models.Toggle>> GetServerToggleOverridesAsync(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        return serverMeta?.Toggles ?? [];
    }

    /// <inheritdoc/>
    public async Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null)
    {
        // Check if global toggle allows override
        var globalToggle = await GetGlobalToggleAsync(toggleName);
        if (globalToggle is not null && !globalToggle.AllowServerOverride)
        {
            _logger.LogWarning("Cannot override toggle {ToggleName} for server {ServerId} - global override not allowed",
                toggleName, serverId);
            return;
        }

        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {ServerId}", serverId);
            return;
        }

        serverMeta.Toggles ??= [];
        var toggle = serverMeta.Toggles.FirstOrDefault(t => t.Name == toggleName);

        if (toggle is not null)
        {
            toggle.IsEnabled = isEnabled;
            toggle.Description = description ?? toggle.Description;
        }
        else
        {
            toggle = new Models.Toggle
            {
                Id = $"{serverId}_{toggleName}",
                ServerId = serverId,
                Name = toggleName,
                IsEnabled = isEnabled,
                Description = description ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            serverMeta.Toggles.Add(toggle);
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        _logger.LogInformation("Server {ServerId} toggle {ToggleName} set to {IsEnabled}", serverId, toggleName, isEnabled);
    }

    /// <inheritdoc/>
    public async Task SetServerTogglesBulkAsync(ulong serverId, Dictionary<string, (bool IsEnabled, string? Description)> toggles)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta is null || !serverMeta.IsActive)
        {
            _logger.LogWarning("Server meta not found or inactive for serverId {ServerId}", serverId);
            return;
        }

        serverMeta.Toggles ??= [];

        foreach (var (toggleName, (isEnabled, description)) in toggles)
        {
            // Check if global toggle allows override
            var globalToggle = await GetGlobalToggleAsync(toggleName);
            if (globalToggle is not null && !globalToggle.AllowServerOverride)
            {
                _logger.LogDebug("Skipping toggle {ToggleName} - global override not allowed", toggleName);
                continue;
            }

            var toggle = serverMeta.Toggles.FirstOrDefault(t => t.Name == toggleName);
            if (toggle is not null)
            {
                toggle.IsEnabled = isEnabled;
                toggle.Description = description ?? toggle.Description;
            }
            else
            {
                serverMeta.Toggles.Add(new Models.Toggle
                {
                    Id = $"{serverId}_{toggleName}",
                    ServerId = serverId,
                    Name = toggleName,
                    IsEnabled = isEnabled,
                    Description = description ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
    }

    /// <inheritdoc/>
    public async Task ResetServerToggleAsync(ulong serverId, string toggleName)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta?.Toggles is null)
        {
            return;
        }

        var toggle = serverMeta.Toggles.FirstOrDefault(t => t.Name == toggleName);
        if (toggle is not null)
        {
            serverMeta.Toggles.Remove(toggle);
            serverMeta.LastUpdated = DateTime.UtcNow;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

            _logger.LogInformation("Server {ServerId} toggle {ToggleName} reset to global default", serverId, toggleName);
        }
    }

    /// <inheritdoc/>
    public async Task ResetAllServerTogglesAsync(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta?.Toggles is null || serverMeta.Toggles.Count == 0)
        {
            return;
        }

        serverMeta.Toggles.Clear();
        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        _logger.LogInformation("Server {ServerId} all toggles reset to global defaults", serverId);
    }

    // ========== Legacy/Utility Methods ==========

    /// <inheritdoc/>
    public async Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId)
    {
        return await GetServerToggleOverridesAsync(serverId);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAllTogglesAsync(string toggleName, bool isEnabled, string? description = null)
    {
        // Update global toggle
        await SetGlobalToggleAsync(toggleName, isEnabled, description);

        _logger.LogInformation("Updated global toggle {ToggleName} for all servers", toggleName);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> RemoveObsoleteTogglesAsync(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        if (serverMeta?.Toggles is null || serverMeta.Toggles.Count == 0)
        {
            return 0;
        }

        var validToggleNames = new HashSet<string>(Constants.ToggleNames.Toggles);
        var obsoleteToggles = serverMeta.Toggles
            .Where(t => !validToggleNames.Contains(t.Name))
            .ToList();

        if (obsoleteToggles.Count == 0)
        {
            return 0;
        }

        foreach (var toggle in obsoleteToggles)
        {
            serverMeta.Toggles.Remove(toggle);
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        _logger.LogInformation("Removed {Count} obsolete toggles for server {ServerId}: [{Toggles}]",
            obsoleteToggles.Count, serverId, string.Join(", ", obsoleteToggles.Select(t => t.Name)));

        return obsoleteToggles.Count;
    }

    // ========== Private Helper Methods ==========

    private async Task ExecuteWithGlobalToggleRepositoryAsync(Func<IGlobalToggleRepository, Task> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGlobalToggleRepository>();
        await operation(repo);
    }

    private async Task<T> ExecuteWithGlobalToggleRepositoryAsync<T>(Func<IGlobalToggleRepository, Task<T>> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGlobalToggleRepository>();
        return await operation(repo);
    }

    private static string GetDefaultToggleDescription(string toggleName) => toggleName switch
    {
        Constants.ToggleNames.EnableTTS => "Enable text-to-speech functionality",
        Constants.ToggleNames.EnableJoinMessage => "Enable welcome messages when users join",
        Constants.ToggleNames.EnableChat => "Enable AI chat responses",
        Constants.ToggleNames.EnableLiveJob => "Enable live activity tracking",
        Constants.ToggleNames.EnableAIWelcome => "Enable AI-generated welcome messages",
        Constants.ToggleNames.EnableNSFW => "Enable NSFW content features",
        Constants.ToggleNames.EnableDailyNSFW => "Enable daily NSFW content posting",
        _ => string.Empty
    };

    private static string GetToggleCategory(string toggleName) => toggleName switch
    {
        Constants.ToggleNames.EnableTTS => "Voice",
        Constants.ToggleNames.EnableJoinMessage => "Welcome",
        Constants.ToggleNames.EnableChat => "Chat",
        Constants.ToggleNames.EnableLiveJob => "Activity",
        Constants.ToggleNames.EnableAIWelcome => "Welcome",
        Constants.ToggleNames.EnableNSFW => "Content",
        Constants.ToggleNames.EnableDailyNSFW => "Content",
        _ => "General"
    };
}
