using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Meta;

/// <summary>
/// Service implementation for managing server metadata operations.
/// Handles caching, database operations, and server-specific configuration management.
/// Implements thread-safe operations using semaphores for concurrent access control.
/// </summary>
public class ServerMetaService : IServerMetaService
{
    private readonly ILogger<IServerMetaService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IServerMetaRepository _serverMetaRepository;

    // Semaphore dictionary to manage access to ServerMeta objects
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _serverMetaSemaphores = new();

    /// <summary>
    /// Initializes a new instance of the ServerMetaService.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed server metadata.</param>
    /// <param name="serverMetaRepository">Repository for database operations on server metadata.</param>
    public ServerMetaService(ILogger<IServerMetaService> logger, IMemoryCache memoryCache, IServerMetaRepository serverMetaRepository)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _serverMetaRepository = serverMetaRepository;
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId)
    {
        return await GetServerMetaInternalAsync(serverId);
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId, bool includeToggles)
    {
        var serverMeta = await GetServerMetaInternalAsync(serverId);

        if (includeToggles)
        {
            await EnsureTogglesLoadedAsync(serverMeta, serverId);
        }

        return serverMeta;
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ExtendedShardedInteractionContext context)
    {
        var serverId = context.Guild?.Id ?? 0;
        if (serverId == 0)
        {
            throw new ArgumentException("ServerId cannot be zero.");
        }

        var cacheKey = GetServerMetaCacheKey(serverId);
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

    /// <inheritdoc/>
    public async Task<Models.ServerMeta> CreateServerMetaAsync(ulong serverId, string serverName)
    {
        if (serverId == 0)
        {
            throw new ArgumentException("ServerId cannot be zero.");
        }

        var cacheKey = GetServerMetaCacheKey(serverId);
        if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? existingMeta) && existingMeta is not null)
        {
            return existingMeta;
        }

        var existingServerMeta = await _serverMetaRepository.AsQueryable()
            .FirstOrDefaultAsync(x => x.Id == serverId);

        if (existingServerMeta is not null)
        {
            _logger.LogWarning("ServerMeta already exists for serverId {serverId} - {serverName}", serverId, serverName);
            existingServerMeta.ServerName = serverName;
            existingServerMeta.LastUpdated = DateTime.UtcNow;
            existingServerMeta.IsActive = true;

            await UpdateServerMetaAsync(existingServerMeta);
            _memoryCache.Set(cacheKey, existingServerMeta, TimeSpan.FromMinutes(30));

            return existingServerMeta;
        }

        var serverMeta = new Models.ServerMeta
        {
            Id = serverId,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            ServerName = serverName,
            Persona = string.Empty,
            Toggles = [],
            Messages = [],
            CommandLogs = [],
            NachoPacks = []
        };

        _logger.LogInformation("Creating new ServerMeta for serverId {serverId}", serverId);

        await _serverMetaRepository.AddAsync(serverMeta);
        await _serverMetaRepository.SaveChangesAsync();

        _memoryCache.Set(cacheKey, serverMeta, TimeSpan.FromMinutes(30)); // Cache for 30 minutes
        return serverMeta;
    }

    /// <inheritdoc/>
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
            var meta = await _serverMetaRepository.AsQueryable()
                .Include(x => x.Toggles)
                .FirstOrDefaultAsync(x => x.Id == serverMeta.Id)
                ?? throw new Exception($"ServerMeta not found for serverId {serverMeta.Id}");

            meta.ServerName = serverMeta.ServerName;
            meta.Persona = serverMeta.Persona;
            meta.LastUpdated = DateTime.UtcNow;
            meta.IsActive = serverMeta.IsActive;

            // Merge Toggles manually
            if (serverMeta.Toggles is not null)
            {
                foreach (var incomingToggle in serverMeta.Toggles)
                {
                    var existingToggle = meta.Toggles?
                        .FirstOrDefault(x => x.Name == incomingToggle.Name);

                    if (existingToggle is not null)
                    {
                        existingToggle.IsEnabled = incomingToggle.IsEnabled;
                        existingToggle.Description = incomingToggle.Description;
                    }
                    else
                    {
                        meta.Toggles?.Add(new Models.Toggle
                        {
                            Id = Guid.NewGuid().ToString(),
                            ServerId = serverMeta.Id,
                            Name = incomingToggle.Name,
                            IsEnabled = incomingToggle.IsEnabled,
                            Description = incomingToggle.Description,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _serverMetaRepository.SaveChangesAsync();
            _memoryCache.Set(GetServerMetaCacheKey(serverMeta.Id), meta, TimeSpan.FromMinutes(30));
            _memoryCache.Remove(StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverMeta.Id.ToString()));
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc/>
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
            _memoryCache.Remove(GetServerMetaCacheKey(serverId)); // Remove from cache
        }
        finally
        {
            semaphore.Release();
            _serverMetaSemaphores.TryRemove(serverId, out _); // Clean up semaphore
        }
    }

    /// <inheritdoc/>
    public async Task<List<Models.ServerMeta>> GetAllServerMetasAsync()
    {
        _logger.LogInformation("Fetching all ServerMetas from repository");
        return await _serverMetaRepository.AsQueryable().ToListAsync();
    }

    // Private methods

    /// <summary>
    /// Internal method for retrieving server metadata with caching support.
    /// Fetches from cache first, then from database if not cached.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve metadata for.</param>
    /// <returns>The server metadata if found; otherwise, null.</returns>
    private async Task<Models.ServerMeta?> GetServerMetaInternalAsync(ulong serverId)
    {
        var cacheKey = GetServerMetaCacheKey(serverId);
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

    /// <summary>
    /// Ensures that toggles are loaded for the specified server metadata.
    /// Creates default toggles if none exist.
    /// </summary>
    /// <param name="serverMeta">The server metadata to ensure toggles are loaded for.</param>
    /// <param name="serverId">The Discord server ID for logging and operations.</param>
    private async Task EnsureTogglesLoadedAsync(Models.ServerMeta? serverMeta, ulong serverId)
    {
        if (serverMeta is null)
        {
            _logger.LogWarning("ServerMeta is null for serverId {serverId}", serverId);
            return;
        }

        if (serverMeta.Toggles is null || serverMeta.Toggles.Count == 0)
        {
            _logger.LogInformation("Toggles not loaded for serverId {serverId}, loading from repository", serverId);
            serverMeta.Toggles = await _serverMetaRepository.AsQueryable()
                .Where(x => x.Id == serverId)
                .SelectMany(x => x.Toggles!)
                .ToListAsync();
        }

        if (serverMeta.Toggles is null)
        {
            _logger.LogInformation("Toggles are null for serverId {serverId}, seeding toggles", serverId);
            await CreateDefaultTogglesForServerAsync(serverMeta.Id);

            serverMeta.Toggles = await _serverMetaRepository.AsQueryable()
                .Where(x => x.Id == serverId)
                .SelectMany(x => x.Toggles!)
                .ToListAsync();
        }
    }

    /// <summary>
    /// Creates default toggles for a server if they don't exist.
    /// Uses the predefined toggle names from Constants.ToggleNames.Toggles.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create toggles for.</param>
    private async Task CreateDefaultTogglesForServerAsync(ulong serverId)
    {
        var existingServerMeta = await _serverMetaRepository.AsQueryable()
            .Include(x => x.Toggles)
            .FirstOrDefaultAsync(x => x.Id == serverId);

        if (existingServerMeta is null)
        {
            _logger.LogWarning("ServerMeta not found for serverId {serverId} when creating default toggles", serverId);
            return;
        }

        var expectedToggles = Constants.ToggleNames.Toggles;
        existingServerMeta.Toggles ??= new List<Models.Toggle>();

        var existingToggleNames = existingServerMeta.Toggles.Select(t => t.Name).ToHashSet();
        var missingToggles = expectedToggles.Where(toggleName => !existingToggleNames.Contains(toggleName)).ToList();

        foreach (var toggleName in missingToggles)
        {
            var toggle = new Models.Toggle
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Name = toggleName,
                IsEnabled = true,
                Description = string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            existingServerMeta.Toggles.Add(toggle);
        }

        if (missingToggles.Any())
        {
            existingServerMeta.LastUpdated = DateTime.UtcNow;
            await _serverMetaRepository.SaveChangesAsync();
            _logger.LogInformation("Created {count} default toggles for serverId {serverId}", missingToggles.Count, serverId);
        }
    }

    /// <summary>
    /// Generates a cache key for server metadata.
    /// </summary>
    /// <param name="serverId">The Discord server ID to generate a cache key for.</param>
    /// <returns>A formatted cache key string.</returns>
    private string GetServerMetaCacheKey(ulong serverId)
    {
        return StringModifier.CreateCacheKey(Constants.CacheKeys.ServerMeta, serverId.ToString());
    }
}