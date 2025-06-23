namespace Amiquin.Core.Services.Chat.Toggle;

public interface IToggleService
{
    Task<bool> IsEnabledAsync(ulong serverId, string toggleName);
    Task CreateServerTogglesIfNotExistsAsync(ulong serverId);
    Task<List<Models.Toggle>> GetTogglesByServerId(ulong serverId);
    Task SetServerToggleAsync(ulong serverId, string toggleName, bool isEnabled, string? description = null);
    Task SetServerTogglesBulkAsync(ulong serverId, Dictionary<string, (bool IsEnabled, string? Description)> toggles);
    Task<bool> UpdateAllTogglesAsync(string toggleName, bool isEnabled, string? description = null);
}
