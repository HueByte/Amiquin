using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat.Toggle;

public class ToggleService : IToggleService
{
    private readonly ILogger<ToggleService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IToggleRepository _toggleRepository;

    public ToggleService(ILogger<ToggleService> logger, IMemoryCache memoryCache, IToggleRepository toggleRepository)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _toggleRepository = toggleRepository;
    }

    public async Task CreateServerTogglesIfNotExistsAsync(ulong serverId, bool useCache = true)
    {
        var cacheKey = $"{serverId}::{Constants.CacheKeys.ServerTogglesCreated}";
        if (useCache && _memoryCache.TryGetValue(cacheKey, out var created))
        {
            if (created is bool value && value)
            {
                return;
            }
        }

        var serverToggles = await GetTogglesByServerId(serverId);
        var expectedToggles = Constants.ToggleNames.Toggles;

        foreach (var toggle in expectedToggles)
        {
            var toggleName = $"{serverId}::{toggle}";
            if (serverToggles.All(x => x.Name != toggleName))
            {
                var toggleValue = true;
                _logger.LogInformation("Creating toggle {toggleName} with value {value}", toggleName, toggleValue);
                await SetServerToggleAsync(toggle, toggleValue, serverId);
            }
        }

        _memoryCache.Set(cacheKey, true, TimeSpan.FromDays(1));
    }

    /// <summary>
    /// Checks if toggle is enabled based on the hierarchy<br>
    /// System -> Server
    /// </summary>
    /// <param name="toggleName"></param>
    /// <param name="serverId"></param>
    /// <returns></returns>
    public async Task<bool> IsEnabledAsync(string toggleName, ulong serverId)
    {
        var systemToggleName = $"{Constants.ToggleNames.SystemTogglePrefix}{toggleName}";
        var systemToggleValue = await GetToggleValueAsync(systemToggleName);
        if (systemToggleValue is not null)
        {
            _logger.LogInformation("System toggle override exists [{toggleName} = {value}]", toggleName, systemToggleValue.Value);
            return systemToggleValue.Value;
        }

        var serverToggleName = $"{serverId}::{toggleName}";
        var serverToggleValue = await GetToggleValueAsync(serverToggleName);
        if (serverToggleValue is not null)
        {
            return serverToggleValue.Value;
        }

        return false;
    }

    public async Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId)
    {
        return await _toggleRepository.AsQueryable()
            .Where(x => x.Name.StartsWith($"{serverId}::") && x.Scope == ToggleScope.Server)
            .ToListAsync();
    }

    public async Task<List<Models.Toggle>> GetTogglesByScopeAsync(ToggleScope scope)
    {
        return await _toggleRepository.AsQueryable()
            .Where(x => x.Scope == scope)
            .ToListAsync();
    }

    public async Task<bool?> GetToggleValueAsync(string toggleName)
    {
        if (_memoryCache.TryGetValue(toggleName, out Models.Toggle? toggle) && toggle is not null)
        {
            return toggle.IsEnabled;
        }

        var toggleValue = await _toggleRepository.AsQueryable().FirstOrDefaultAsync(x => x.Name == toggleName);
        if (toggleValue is not null)
        {
            _memoryCache.Set(toggleName, toggleValue, TimeSpan.FromDays(1));
            return toggleValue.IsEnabled;
        }

        return null;
    }

    public async Task<bool?> GetToggleValueByIdAsync(string id)
    {
        if (_memoryCache.TryGetValue(id, out bool isEnabled))
        {
            return isEnabled;
        }

        var toggleValue = await _toggleRepository.AsQueryable().FirstOrDefaultAsync(x => x.Name == id);
        if (toggleValue is not null)
        {
            _memoryCache.Set(id, toggleValue.IsEnabled, TimeSpan.FromDays(1));
            return toggleValue.IsEnabled;
        }

        return null;
    }

    public async Task SetServerToggleAsync(string toggleName, bool isEnabled, ulong serverId, string? description = null)
    {
        toggleName = $"{serverId}::{toggleName}";
        await SetToggleInternalAsync(toggleName, isEnabled, description, ToggleScope.Server);
    }

    public async Task SetSystemToggleAsync(string toggleName, bool isEnabled, string? description = null)
    {
        toggleName = $"System::{toggleName}";
        await SetToggleInternalAsync(toggleName, isEnabled, description, ToggleScope.Global);
    }

    public async Task RemoveServerToggleAsync(string toggleName, ulong serverId)
    {
        toggleName = $"{serverId}::{toggleName}";
        await RemoveToggleInternalAsync(toggleName, ToggleScope.Server);
    }

    public async Task RemoveSystemToggleAsync(string toggleName)
    {
        toggleName = $"System::{toggleName}";
        await RemoveToggleInternalAsync(toggleName, ToggleScope.Global);
    }

    private async Task SetToggleInternalAsync(string toggleName, bool isEnabled, string? description = null, ToggleScope scope = ToggleScope.Global)
    {
        _logger.LogInformation("Setting toggle {toggleName} to {isEnabled}", toggleName, isEnabled);
        var toggle = await _toggleRepository.AsQueryable().FirstOrDefaultAsync(x => x.Name == toggleName);
        if (toggle is not null)
        {
            toggle.IsEnabled = isEnabled;
            toggle.Scope = scope;
            toggle.Description = description ?? string.Empty;
            await _toggleRepository.UpdateAsync(toggle);
        }
        else
        {
            toggle = new Models.Toggle
            {
                Id = Guid.NewGuid().ToString(),
                Name = toggleName,
                IsEnabled = isEnabled,
                CreatedAt = DateTime.UtcNow,
                Description = description ?? string.Empty,
                Scope = scope
            };

            await _toggleRepository.AddAsync(toggle);
        }

        _memoryCache.Set(toggleName, isEnabled, TimeSpan.FromDays(1));
        await _toggleRepository.SaveChangesAsync();
    }

    private async Task RemoveToggleInternalAsync(string toggleName, ToggleScope scope)
    {
        var toggle = await _toggleRepository.AsQueryable().FirstOrDefaultAsync(x => x.Name == toggleName && x.Scope == scope);
        if (toggle is null)
        {
            _logger.LogWarning("Toggle {toggleName} not found", toggleName);
            _memoryCache.Remove(toggleName);
            return;
        }

        await _toggleRepository.RemoveAsync(toggle);
        await _toggleRepository.SaveChangesAsync();
        _memoryCache.Remove(toggleName);

        _logger.LogInformation("Removed toggle {toggleName}", toggleName);
    }
}