namespace Amiquin.Core.Services.Nacho;

public interface INachoService
{
    Task AddNachoAsync(ulong userId, ulong serverId, int nachoCount = 1);
    Task<int> GetServerNachoCountAsync(ulong serverId);
    Task<int> GetTotalNachoCountAsync();
    Task<int> GetUserNachoCountAsync(ulong userId);
    Task RemoveAllNachoAsync(ulong userId);
    Task RemoveAllServerNachoAsync(ulong serverId);
    Task RemoveNachoAsync(ulong userId, ulong serverId);
}
