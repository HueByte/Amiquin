using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Chat.Toggle;

public interface IToggleService
{
    Task<bool> IsEnabledAsync(string toggleName, ulong serverId);
    Task CreateServerTogglesIfNotExistsAsync(ulong serverId, bool useCache = true);
    Task<List<Models.Toggle>> GetTogglesByScopeAsync(ToggleScope scope);
    Task<bool?> GetToggleValueAsync(string toggleName);
    Task<bool?> GetToggleValueByIdAsync(string id);
    Task SetSystemToggleAsync(string toggleName, bool isEnabled, string? description = null);
    Task SetServerToggleAsync(string toggleName, bool isEnabled, ulong serverId, string? description = null);
    Task RemoveSystemToggleAsync(string toggleName);
    Task RemoveServerToggleAsync(string toggleName, ulong serverId);
}
