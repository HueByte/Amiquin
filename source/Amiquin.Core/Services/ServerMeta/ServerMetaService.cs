using System.Collections.Concurrent;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ServerMeta;

public class ServerMetaService : IServerMetaService
{
    private readonly ILogger<IServerMetaService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IServerMetaRepository _serverMetaRepository;

    // Semaphore dictionary to manage access to ServerMeta objects
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _serverMetaSemaphores = new();

    public ServerMetaService(ILogger<IServerMetaService> logger, IMemoryCache memoryCache, IServerMetaRepository serverMetaRepository)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _serverMetaRepository = serverMetaRepository;
    }

    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId)
    {
        return await GetServerMetaInternalAsync(serverId);
    }

    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId, bool includeToggles)
    {
        var serverMeta = await GetServerMetaInternalAsync(serverId);

        if (includeToggles)
        {
            await EnsureTogglesLoadedAsync(serverMeta, serverId);
        }

        return serverMeta;
    }

    private async Task<Models.ServerMeta?> GetServerMetaInternalAsync(ulong serverId)
    {
        var cacheKey = GetCacheKey(serverId);
        if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? serverMeta) && serverMeta is not null)
        {
            return serverMeta;
        }

        _logger.LogInformation("Fetching ServerMeta for serverId {serverId} from repository", serverId);

        serverMeta = await _serverMetaRepository.AsQueryable()
            .FirstOrDefaultAsync(x => x.Id == serverId);

        if (serverMeta is null)
        {
            _logger.LogWarning("Server meta not found for serverId {serverId}", serverId);
            return null;
        }

        _memoryCache.Set(cacheKey, serverMeta, TimeSpan.FromMinutes(30)); // Cache for 30 minutes
        return serverMeta;
    }

    private async Task EnsureTogglesLoadedAsync(Models.ServerMeta? serverMeta, ulong serverId)
    {
        if (serverMeta is not null && serverMeta.Toggles?.Count == 0)
        {
            serverMeta.Toggles = await _serverMetaRepository.AsQueryable()
                .Where(x => x.Id == serverId)
                .SelectMany(x => x.Toggles ?? Enumerable.Empty<Models.Toggle>())
                .ToListAsync();
        }
    }

    public async Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ExtendedShardedInteractionContext context)
    {
        var serverId = context.Guild?.Id ?? 0;
        var cacheKey = GetCacheKey(serverId);
        if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? serverMeta) && serverMeta is not null)
        {
            return serverMeta;
        }

        serverMeta = await _serverMetaRepository.AsQueryable()
                .FirstOrDefaultAsync(x => x.Id == serverId);

        if (serverMeta is null)
        {
            serverMeta = new Models.ServerMeta
            {
                Id = serverId,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                IsActive = true,
                ServerName = context.Guild?.Name ?? string.Empty,
                Persona = string.Empty,
                Toggles = [],
                Messages = [],
                CommandLogs = [],
                NachoPacks = []
            };

            _logger.LogInformation("Creating new ServerMeta for serverId {serverId}", serverId);

            await _serverMetaRepository.AddAsync(serverMeta);
            await _serverMetaRepository.SaveChangesAsync();
        }

        _memoryCache.Set(cacheKey, serverMeta, TimeSpan.FromMinutes(30)); // Cache for 30 minutes
        return serverMeta;
    }

    public async Task UpdateServerMetaAsync(Models.ServerMeta serverMeta)
    {
        _logger.LogInformation("Updating ServerMeta for serverId {serverId}", serverMeta.Id);

        if (serverMeta.Id == 0)
        {
            throw new ArgumentException("ServerId must be set before updating ServerMeta.");
        }

        var semaphore = _serverMetaSemaphores.GetOrAdd(serverMeta.Id, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            var existingMeta = await _serverMetaRepository.GetAsync(serverMeta.Id);
            if (existingMeta is null)
            {
                await _serverMetaRepository.AddAsync(serverMeta);
            }
            else
            {
                existingMeta.ServerName = serverMeta.ServerName;
                existingMeta.Persona = serverMeta.Persona;
                existingMeta.LastUpdated = DateTime.UtcNow;
                existingMeta.IsActive = serverMeta.IsActive;
            }

            await _serverMetaRepository.SaveChangesAsync();
            _memoryCache.Set(GetCacheKey(serverMeta.Id), serverMeta, TimeSpan.FromMinutes(30)); // Update cache
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task DeleteServerMetaAsync(ulong serverId)
    {
        _logger.LogInformation("Deleting ServerMeta for serverId {serverId}", serverId);

        var semaphore = _serverMetaSemaphores.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync();
        try
        {
            var serverMeta = await GetServerMetaAsync(serverId) ?? throw new Exception($"ServerMeta not found for serverId {serverId}");

            await _serverMetaRepository.RemoveAsync(serverMeta);
            await _serverMetaRepository.SaveChangesAsync();
            _memoryCache.Remove(GetCacheKey(serverId)); // Remove from cache
        }
        finally
        {
            semaphore.Release();
            _serverMetaSemaphores.TryRemove(serverId, out _); // Clean up semaphore
        }
    }

    public async Task<List<Models.ServerMeta>> GetAllServerMetasAsync()
    {
        _logger.LogInformation("Fetching all ServerMetas from repository");
        return await _serverMetaRepository.AsQueryable().ToListAsync();
    }

    private string GetCacheKey(ulong serverId)
    {
        return StringModifier.CreateCacheKey(Constants.CacheKeys.ServerMeta, serverId.ToString());
    }
}