using Amiquin.Core.Models;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Utilities;
using Amiquin.Core.Utilities.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Toggle;

/// <summary>
/// Service implementation for managing server-scoped toggle operations.
/// All toggles are server-specific with no global override system.
/// </summary>
public class ToggleService : IToggleService
{
    private readonly ILogger<ToggleService> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IMemoryCache _memoryCache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public ToggleService(
        ILogger<ToggleService> logger,
        IServerMetaService serverMetaService,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _memoryCache = memoryCache;
    }

    // ========== Toggle State Operations ==========

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(ulong serverId, string toggleName)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        var toggle = serverMeta?.Toggles?.FirstOrDefault(t => t.Name == toggleName);
        return toggle?.IsEnabled ?? false;
    }

    /// <inheritdoc/>
    public async Task<ToggleState> GetToggleStateAsync(ulong serverId, string toggleName)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        var toggle = serverMeta?.Toggles?.FirstOrDefault(t => t.Name == toggleName);

        if (toggle is not null)
        {
            return new ToggleState
            {
                Name = toggleName,
                IsEnabled = toggle.IsEnabled,
                Description = toggle.Description,
                Category = GetToggleCategory(toggleName)
            };
        }

        // Toggle not configured - return default state
        return new ToggleState
        {
            Name = toggleName,
            IsEnabled = false,
            Description = GetDefaultToggleDescription(toggleName),
            Category = GetToggleCategory(toggleName)
        };
    }

    /// <inheritdoc/>
    public async Task<List<ToggleState>> GetAllTogglesAsync(ulong serverId)
    {
        var result = new List<ToggleState>();
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        var serverToggleDict = serverMeta?.Toggles?.ToDictionary(t => t.Name, t => t) ?? new Dictionary<string, Models.Toggle>();

        // Use all defined toggles from constants
        foreach (var toggleName in Constants.ToggleNames.Toggles)
        {
            serverToggleDict.TryGetValue(toggleName, out var serverToggle);

            result.Add(new ToggleState
            {
                Name = toggleName,
                IsEnabled = serverToggle?.IsEnabled ?? false,
                Description = serverToggle?.Description ?? GetDefaultToggleDescription(toggleName),
                Category = GetToggleCategory(toggleName)
            });
        }

        return result.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
    }

    // ========== Server Toggle Operations ==========

    /// <inheritdoc/>
    public async Task CreateServerTogglesIfNotExistsAsync(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetOrCreateServerMetaAsync(serverId);

        serverMeta.Toggles ??= [];

        var existingToggleNames = serverMeta.Toggles.Select(t => t.Name).ToHashSet();
        var missingToggles = Constants.ToggleNames.Toggles.Where(t => !existingToggleNames.Contains(t)).ToList();

        if (missingToggles.Count == 0)
        {
            _logger.LogDebug("Server {ServerId} already has all toggles", serverId);
            return;
        }

        foreach (var toggleName in missingToggles)
        {
            serverMeta.Toggles.Add(new Models.Toggle
            {
                Id = $"{serverId}_{toggleName}",
                ServerId = serverId,
                Name = toggleName,
                IsEnabled = false,
                Description = GetDefaultToggleDescription(toggleName),
                CreatedAt = DateTime.UtcNow
            });
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        _logger.LogInformation("Created {Count} toggles for server {ServerId}: {Toggles}",
            missingToggles.Count, serverId, string.Join(", ", missingToggles));
    }

    /// <inheritdoc/>
    public async Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null)
    {
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
                Description = description ?? GetDefaultToggleDescription(toggleName),
                CreatedAt = DateTime.UtcNow
            };
            serverMeta.Toggles.Add(toggle);
        }

        serverMeta.LastUpdated = DateTime.UtcNow;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        _logger.LogInformation("Server {ServerId} toggle {ToggleName} set to {IsEnabled}", serverId, toggleName, isEnabled);
    }

    /// <inheritdoc/>
    public async Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId, includeToggles: true);
        return serverMeta?.Toggles ?? [];
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
