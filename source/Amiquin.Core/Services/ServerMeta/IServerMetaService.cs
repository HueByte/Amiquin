using Amiquin.Core.DiscordExtensions;

namespace Amiquin.Core.Services.ServerMeta;

public interface IServerMetaService
{
    Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId);
    Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId, bool includeToggles = false);
    Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ExtendedShardedInteractionContext context);
    Task DeleteServerMetaAsync(ulong serverId);
    Task<List<Models.ServerMeta>> GetAllServerMetasAsync();
    Task UpdateServerMetaAsync(Models.ServerMeta serverMeta);
}
