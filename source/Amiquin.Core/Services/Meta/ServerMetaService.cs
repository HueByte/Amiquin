using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Amiquin.Core.Services.Meta;

/// <summary>
/// Service implementation for managing server metadata operations with advanced caching and performance optimizations.
/// Handles caching, database operations, server-specific configuration management, and implements
/// thread-safe operations using semaphores for concurrent access control with automatic cleanup.
/// </summary>
public class ServerMetaService : IServerMetaService, IDisposable
{
    private const int DefaultCacheTimeoutMinutes = Constants.Timeouts.DefaultCacheTimeoutMinutes;
    private const int SemaphoreTimeoutSeconds = Constants.Timeouts.SemaphoreTimeoutSeconds;
    private const int MaxConcurrentOperations = 1;
    private const int SemaphoreCleanupIntervalMinutes = Constants.Timeouts.SemaphoreCleanupIntervalMinutes;
    private const string CacheHitMetric = "ServerMeta cache hit";
    private const string CacheMissMetric = "ServerMeta cache miss";
    private const string DatabaseQueryMetric = "ServerMeta database query";

    private readonly ILogger<IServerMetaService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceProvider _serviceProvider;

    // Semaphore dictionary to manage access to ServerMeta objects with automatic cleanup
    private readonly ConcurrentDictionary<ulong, SemaphoreEntry> _serverMetaSemaphores = new();

    // Performance tracking
    private readonly ConcurrentDictionary<string, long> _operationMetrics = new();

    // Disposal tracking
    private volatile bool _disposed;
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Initializes a new instance of the ServerMetaService with advanced caching and cleanup mechanisms.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="memoryCache">Memory cache for storing frequently accessed server metadata.</param>
    /// <param name="serviceProvider">Service provider for creating scoped services.</param>
    public ServerMetaService(ILogger<IServerMetaService> logger, IMemoryCache memoryCache, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Set up automatic semaphore cleanup every hour
        _cleanupTimer = new Timer(CleanupUnusedSemaphores, null,
            TimeSpan.FromMinutes(SemaphoreCleanupIntervalMinutes),
            TimeSpan.FromMinutes(SemaphoreCleanupIntervalMinutes));

        _logger.LogDebug("ServerMetaService initialized with cleanup timer interval: {minutes} minutes", SemaphoreCleanupIntervalMinutes);
    }

    /// <summary>
    /// Represents a semaphore entry with tracking information for automatic cleanup.
    /// </summary>
    private sealed class SemaphoreEntry
    {
        public SemaphoreSlim Semaphore { get; }
        public DateTime LastUsed { get; set; }

        public SemaphoreEntry()
        {
            Semaphore = new SemaphoreSlim(MaxConcurrentOperations, MaxConcurrentOperations);
            LastUsed = DateTime.UtcNow;
        }
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId)
    {
        ThrowIfDisposed();
        return await GetServerMetaInternalAsync(serverId);
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta?> GetServerMetaAsync(ulong serverId, bool includeToggles)
    {
        ThrowIfDisposed();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var serverMeta = await GetServerMetaInternalAsync(serverId);

            if (includeToggles && serverMeta is not null)
            {
                await EnsureTogglesLoadedAsync(serverMeta, serverId);
            }

            return serverMeta;
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("GetServerMetaWithToggles", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ExtendedShardedInteractionContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        var serverId = context.Guild?.Id ?? 0;
        if (serverId == 0)
        {
            throw new ArgumentException("ServerId cannot be zero - context must contain a valid guild.", nameof(context));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var cacheKey = GetServerMetaCacheKey(serverId);

            // Check cache first
            if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? serverMeta) && serverMeta is not null)
            {
                RecordMetric(CacheHitMetric, 1);
                return serverMeta;
            }

            RecordMetric(CacheMissMetric, 1);

            // Use semaphore for thread-safe get-or-create operation
            var semaphoreEntry = GetOrCreateSemaphoreEntry(serverId);
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds)).Token;

            if (!await semaphoreEntry.Semaphore.WaitAsync(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds), cancellationToken))
            {
                throw new TimeoutException($"Timeout waiting for semaphore access for serverId {serverId}");
            }

            try
            {
                // Double-check cache after acquiring semaphore
                if (_memoryCache.TryGetValue(cacheKey, out serverMeta) && serverMeta is not null)
                {
                    RecordMetric(CacheHitMetric, 1);
                    return serverMeta;
                }

                // Query database
                var queryStopwatch = Stopwatch.StartNew();
                serverMeta = await ExecuteWithRepositoryAsync(async repo =>
                    await repo.AsQueryable().FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken));
                queryStopwatch.Stop();
                RecordMetric(DatabaseQueryMetric, queryStopwatch.ElapsedMilliseconds);

                if (serverMeta is null)
                {
                    // Create new ServerMeta
                    serverMeta = CreateNewServerMeta(serverId, context.Guild?.Name ?? Constants.DefaultValues.UnknownServer);

                    _logger.LogInformation("Creating new ServerMeta for serverId {serverId} with name {serverName}",
                        serverId, serverMeta.ServerName);

                    await ExecuteWithRepositoryAsync(async repo =>
                    {
                        await repo.AddAsync(serverMeta);
                        await repo.SaveChangesAsync();
                    });
                }

                // Cache with sliding expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes / 2),
                    Priority = CacheItemPriority.High
                };

                _memoryCache.Set(cacheKey, serverMeta, cacheOptions);
                return serverMeta;
            }
            finally
            {
                semaphoreEntry.Semaphore.Release();
                semaphoreEntry.LastUsed = DateTime.UtcNow;
            }
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("GetOrCreateServerMeta", stopwatch.ElapsedMilliseconds);
        }
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

        var existingServerMeta = await ExecuteWithRepositoryAsync(async repo =>
            await repo.AsQueryable().FirstOrDefaultAsync(x => x.Id == serverId));

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
            PreferredProvider = null, // Will use global default
            Toggles = [],
            Messages = [],
            CommandLogs = [],
            NachoPacks = []
        };

        _logger.LogInformation("Creating new ServerMeta for serverId {serverId}", serverId);

        await ExecuteWithRepositoryAsync(async repo =>
        {
            await repo.AddAsync(serverMeta);
            await repo.SaveChangesAsync();
        });

        _memoryCache.Set(cacheKey, serverMeta, TimeSpan.FromMinutes(30)); // Cache for 30 minutes
        return serverMeta;
    }

    /// <inheritdoc/>
    public async Task<Models.ServerMeta> GetOrCreateServerMetaAsync(ulong serverId, string? serverName = null)
    {
        ThrowIfDisposed();

        if (serverId == 0)
        {
            throw new ArgumentException("ServerId cannot be zero.", nameof(serverId));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var cacheKey = GetServerMetaCacheKey(serverId);

            // Check cache first
            if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? serverMeta) && serverMeta is not null)
            {
                RecordMetric(CacheHitMetric, 1);
                return serverMeta;
            }

            RecordMetric(CacheMissMetric, 1);

            // Use semaphore for thread-safe get-or-create operation
            var semaphoreEntry = GetOrCreateSemaphoreEntry(serverId);
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds)).Token;

            if (!await semaphoreEntry.Semaphore.WaitAsync(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds), cancellationToken))
            {
                throw new TimeoutException($"Timeout waiting for semaphore access for serverId {serverId}");
            }

            try
            {
                // Double-check cache after acquiring semaphore
                if (_memoryCache.TryGetValue(cacheKey, out serverMeta) && serverMeta is not null)
                {
                    RecordMetric(CacheHitMetric, 1);
                    return serverMeta;
                }

                // Query database
                var queryStopwatch = Stopwatch.StartNew();
                serverMeta = await ExecuteWithRepositoryAsync(async repo =>
                    await repo.AsQueryable().FirstOrDefaultAsync(x => x.Id == serverId, cancellationToken));
                queryStopwatch.Stop();
                RecordMetric(DatabaseQueryMetric, queryStopwatch.ElapsedMilliseconds);

                if (serverMeta is null)
                {
                    // Create new ServerMeta
                    serverMeta = CreateNewServerMeta(serverId, serverName ?? Constants.DefaultValues.UnknownServer);

                    _logger.LogInformation("Creating new ServerMeta for serverId {serverId} with name {serverName}",
                        serverId, serverMeta.ServerName);

                    await ExecuteWithRepositoryAsync(async repo =>
                    {
                        await repo.AddAsync(serverMeta);
                        await repo.SaveChangesAsync();
                    });
                }

                // Cache with sliding expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes / 2),
                    Priority = CacheItemPriority.High
                };

                _memoryCache.Set(cacheKey, serverMeta, cacheOptions);
                return serverMeta;
            }
            finally
            {
                semaphoreEntry.Semaphore.Release();
                semaphoreEntry.LastUsed = DateTime.UtcNow;
            }
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("GetOrCreateServerMetaById", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateServerMetaAsync(Models.ServerMeta serverMeta)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(serverMeta);

        if (serverMeta.Id == 0)
        {
            throw new ArgumentException("ServerId must be set before updating ServerMeta.", nameof(serverMeta));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Updating ServerMeta for serverId {serverId}", serverMeta.Id);

            var semaphoreEntry = GetOrCreateSemaphoreEntry(serverMeta.Id);
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds)).Token;

            if (!await semaphoreEntry.Semaphore.WaitAsync(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds), cancellationToken))
            {
                throw new TimeoutException($"Timeout waiting for semaphore access for serverId {serverMeta.Id}");
            }

            try
            {
                var meta = await ExecuteWithRepositoryAsync(async repo =>
                {
                    var foundMeta = await repo.AsQueryable()
                        .Include(x => x.Toggles)
                        .FirstOrDefaultAsync(x => x.Id == serverMeta.Id, cancellationToken)
                        ?? throw new InvalidOperationException($"ServerMeta not found for serverId {serverMeta.Id}");

                    // Update basic properties
                    foundMeta.ServerName = serverMeta.ServerName ?? foundMeta.ServerName;
                    foundMeta.Persona = serverMeta.Persona ?? foundMeta.Persona;

                    // Only update PreferredProvider if it's explicitly provided (not null)
                    if (serverMeta.PreferredProvider != null)
                    {
                        foundMeta.PreferredProvider = serverMeta.PreferredProvider;
                    }

                    // Update PrimaryChannelId (nullable field, so allow null to be set)
                    foundMeta.PrimaryChannelId = serverMeta.PrimaryChannelId;

                    // Update NsfwChannelId (nullable field, so allow null to be set)
                    foundMeta.NsfwChannelId = serverMeta.NsfwChannelId;

                    foundMeta.LastUpdated = DateTime.UtcNow;
                    foundMeta.IsActive = serverMeta.IsActive;

                    // Merge toggles if provided
                    if (serverMeta.Toggles is not null)
                    {
                        MergeToggles(foundMeta, serverMeta.Toggles, serverMeta.Id);
                    }

                    await repo.SaveChangesAsync();
                    return foundMeta;
                });

                // Update cache with sliding expiration
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes),
                    SlidingExpiration = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes / 2),
                    Priority = CacheItemPriority.High
                };

                _memoryCache.Set(GetServerMetaCacheKey(serverMeta.Id), meta, cacheOptions);

                // Invalidate related caches
                InvalidateRelatedCaches(serverMeta.Id);

                _logger.LogDebug("Updated ServerMeta for serverId {serverId}", serverMeta.Id);
            }
            finally
            {
                semaphoreEntry.Semaphore.Release();
                semaphoreEntry.LastUsed = DateTime.UtcNow;
            }
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("UpdateServerMeta", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteServerMetaAsync(ulong serverId)
    {
        ThrowIfDisposed();

        if (serverId == 0)
        {
            throw new ArgumentException("ServerId cannot be zero.", nameof(serverId));
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Deleting ServerMeta for serverId {serverId}", serverId);

            var semaphoreEntry = GetOrCreateSemaphoreEntry(serverId);
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds)).Token;

            if (!await semaphoreEntry.Semaphore.WaitAsync(TimeSpan.FromSeconds(SemaphoreTimeoutSeconds), cancellationToken))
            {
                throw new TimeoutException($"Timeout waiting for semaphore access for serverId {serverId}");
            }

            try
            {
                var serverMeta = await GetServerMetaAsync(serverId)
                    ?? throw new InvalidOperationException($"ServerMeta not found for serverId {serverId}");

                await ExecuteWithRepositoryAsync(async repo =>
                {
                    await repo.RemoveAsync(serverMeta);
                    await repo.SaveChangesAsync();
                });

                // Remove from cache and invalidate related caches
                _memoryCache.Remove(GetServerMetaCacheKey(serverId));
                InvalidateRelatedCaches(serverId);

                _logger.LogInformation("Successfully deleted ServerMeta for serverId {serverId}", serverId);
            }
            finally
            {
                semaphoreEntry.Semaphore.Release();

                // Clean up semaphore for deleted server
                if (_serverMetaSemaphores.TryRemove(serverId, out var removedEntry))
                {
                    removedEntry.Semaphore.Dispose();
                }
            }
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("DeleteServerMeta", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <inheritdoc/>
    public async Task<List<Models.ServerMeta>> GetAllServerMetasAsync()
    {
        ThrowIfDisposed();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Fetching all ServerMetas from repository");

            var serverMetas = await ExecuteWithRepositoryAsync(async repo =>
                await repo.AsQueryable()
                    .OrderBy(x => x.ServerName)
                    .ToListAsync());

            _logger.LogDebug("Retrieved {count} ServerMetas from repository", serverMetas.Count);
            return serverMetas;
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("GetAllServerMetas", stopwatch.ElapsedMilliseconds);
        }
    }

    // Private methods

    /// <summary>
    /// Executes a repository operation within a scoped service provider.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The operation to execute with the repository.</param>
    /// <returns>The result of the operation.</returns>
    private async Task<T> ExecuteWithRepositoryAsync<T>(Func<IServerMetaRepository, Task<T>> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
        return await operation(repository);
    }

    /// <summary>
    /// Executes a repository operation within a scoped service provider without return value.
    /// </summary>
    /// <param name="operation">The operation to execute with the repository.</param>
    private async Task ExecuteWithRepositoryAsync(Func<IServerMetaRepository, Task> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
        await operation(repository);
    }

    /// <summary>
    /// Internal method for retrieving server metadata with advanced caching support.
    /// Fetches from cache first, then from database if not cached with performance tracking.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve metadata for.</param>
    /// <returns>The server metadata if found; otherwise, null.</returns>
    private async Task<Models.ServerMeta?> GetServerMetaInternalAsync(ulong serverId)
    {
        var cacheKey = GetServerMetaCacheKey(serverId);

        // Check cache first
        if (_memoryCache.TryGetValue(cacheKey, out Models.ServerMeta? serverMeta) && serverMeta is not null)
        {
            RecordMetric(CacheHitMetric, 1);
            return serverMeta;
        }

        RecordMetric(CacheMissMetric, 1);

        var queryStopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Fetching ServerMeta for serverId {serverId} from repository", serverId);

            serverMeta = await ExecuteWithRepositoryAsync(async repo =>
                await repo.AsQueryable().FirstOrDefaultAsync(x => x.Id == serverId));

            if (serverMeta is null)
            {
                _logger.LogDebug("ServerMeta not found for serverId {serverId}", serverId);
                return null;
            }

            // Cache with sliding expiration
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes),
                SlidingExpiration = TimeSpan.FromMinutes(DefaultCacheTimeoutMinutes / 2),
                Priority = CacheItemPriority.Normal
            };

            _memoryCache.Set(cacheKey, serverMeta, cacheOptions);
            return serverMeta;
        }
        finally
        {
            queryStopwatch.Stop();
            RecordMetric(DatabaseQueryMetric, queryStopwatch.ElapsedMilliseconds);
        }
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
            serverMeta.Toggles = await ExecuteWithRepositoryAsync(async repo =>
                await repo.AsQueryable()
                    .Where(x => x.Id == serverId)
                    .SelectMany(x => x.Toggles!)
                    .ToListAsync());
        }

        if (serverMeta.Toggles is null)
        {
            _logger.LogInformation("Toggles are null for serverId {serverId}, seeding toggles", serverId);
            await CreateDefaultTogglesForServerAsync(serverMeta.Id);

            serverMeta.Toggles = await ExecuteWithRepositoryAsync(async repo =>
                await repo.AsQueryable()
                    .Where(x => x.Id == serverId)
                    .SelectMany(x => x.Toggles!)
                    .ToListAsync());
        }
    }

    /// <summary>
    /// Creates default toggles for a server if they don't exist.
    /// Uses the predefined toggle names from Constants.ToggleNames.Toggles.
    /// </summary>
    /// <param name="serverId">The Discord server ID to create toggles for.</param>
    private async Task CreateDefaultTogglesForServerAsync(ulong serverId)
    {
        await ExecuteWithRepositoryAsync(async repo =>
        {
            var existingServerMeta = await repo.AsQueryable()
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
                    IsEnabled = false, // Consistent with ToggleService default values
                    Description = string.Empty,
                    CreatedAt = DateTime.UtcNow
                };

                existingServerMeta.Toggles.Add(toggle);
            }

            if (missingToggles.Any())
            {
                existingServerMeta.LastUpdated = DateTime.UtcNow;
                await repo.SaveChangesAsync();
                _logger.LogInformation("Created {count} default toggles for serverId {serverId}", missingToggles.Count, serverId);
            }
        });
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

    /// <summary>
    /// Gets or creates a semaphore entry for thread-safe operations on a specific server.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <returns>A semaphore entry for the specified server.</returns>
    private SemaphoreEntry GetOrCreateSemaphoreEntry(ulong serverId)
    {
        return _serverMetaSemaphores.GetOrAdd(serverId, _ => new SemaphoreEntry());
    }

    /// <summary>
    /// Creates a new ServerMeta instance with default values.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    /// <param name="serverName">The name of the Discord server.</param>
    /// <returns>A new ServerMeta instance.</returns>
    private static Models.ServerMeta CreateNewServerMeta(ulong serverId, string serverName)
    {
        return new Models.ServerMeta
        {
            Id = serverId,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            IsActive = true,
            ServerName = serverName,
            Persona = string.Empty,
            PreferredProvider = null, // Will use global default
            Toggles = [],
            Messages = [],
            CommandLogs = [],
            NachoPacks = []
        };
    }

    /// <summary>
    /// Merges incoming toggles with existing toggles in the server metadata.
    /// </summary>
    /// <param name="existingMeta">The existing server metadata.</param>
    /// <param name="incomingToggles">The incoming toggles to merge.</param>
    /// <param name="serverId">The Discord server ID for logging.</param>
    private void MergeToggles(Models.ServerMeta existingMeta, ICollection<Models.Toggle> incomingToggles, ulong serverId)
    {
        existingMeta.Toggles ??= [];

        foreach (var incomingToggle in incomingToggles)
        {
            var existingToggle = existingMeta.Toggles.FirstOrDefault(x => x.Name == incomingToggle.Name);

            if (existingToggle is not null)
            {
                existingToggle.IsEnabled = incomingToggle.IsEnabled;
                existingToggle.Description = incomingToggle.Description ?? existingToggle.Description;
            }
            else
            {
                existingMeta.Toggles.Add(new Models.Toggle
                {
                    Id = Guid.NewGuid().ToString(),
                    ServerId = serverId,
                    Name = incomingToggle.Name,
                    IsEnabled = incomingToggle.IsEnabled,
                    Description = incomingToggle.Description ?? string.Empty,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }

    /// <summary>
    /// Invalidates caches related to the specified server.
    /// </summary>
    /// <param name="serverId">The Discord server ID.</param>
    private void InvalidateRelatedCaches(ulong serverId)
    {
        var serverIdString = serverId.ToString();
        var computedSystemCacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverIdString);
        var serverTogglesCacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ServerToggles, serverIdString);

        _memoryCache.Remove(computedSystemCacheKey);
        _memoryCache.Remove(serverTogglesCacheKey);

        _logger.LogDebug("Invalidated related caches for serverId {serverId}", serverId);
    }

    /// <summary>
    /// Records a performance metric for monitoring and debugging.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="value">The metric value (usually duration in milliseconds or count).</param>
    private void RecordMetric(string operationName, long value)
    {
        _operationMetrics.AddOrUpdate(operationName, value, (key, oldValue) => oldValue + value);

        // Log significant operations for monitoring
        if (value > Constants.Limits.SlowOperationThresholdMs) // Log operations taking more than 1 second
        {
            _logger.LogWarning("Slow ServerMetaService operation: {operationName} took {duration}ms", operationName, value);
        }
    }

    /// <summary>
    /// Cleans up unused semaphores to prevent memory leaks.
    /// </summary>
    /// <param name="state">Timer callback state (unused).</param>
    private void CleanupUnusedSemaphores(object? state)
    {
        if (_disposed) return;

        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-SemaphoreCleanupIntervalMinutes);
            var entriesToRemove = new List<ulong>();

            foreach (var kvp in _serverMetaSemaphores)
            {
                if (kvp.Value.LastUsed < cutoffTime && kvp.Value.Semaphore.CurrentCount == MaxConcurrentOperations)
                {
                    entriesToRemove.Add(kvp.Key);
                }
            }

            foreach (var serverId in entriesToRemove)
            {
                if (_serverMetaSemaphores.TryRemove(serverId, out var entry))
                {
                    entry.Semaphore.Dispose();
                }
            }

            if (entriesToRemove.Count > 0)
            {
                _logger.LogDebug("Cleaned up {count} unused semaphores", entriesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during semaphore cleanup");
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if the service has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Gets performance metrics for monitoring and debugging.
    /// </summary>
    /// <returns>A dictionary containing operation metrics.</returns>
    public IReadOnlyDictionary<string, long> GetPerformanceMetrics()
    {
        ThrowIfDisposed();
        return _operationMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Clears all cached server metadata. Use with caution.
    /// </summary>
    public void ClearAllCache()
    {
        ThrowIfDisposed();

        // TODO: Implement a more sophisticated cache clearing strategy if needed
        // This is a simplified approach - in a real implementation you might want to track cache keys
        if (_memoryCache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Remove all entries
        }

        _logger.LogInformation("Cleared all ServerMeta cache entries");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cleanupTimer?.Dispose();

            // Dispose all semaphores
            foreach (var entry in _serverMetaSemaphores.Values)
            {
                entry.Semaphore.Dispose();
            }
            _serverMetaSemaphores.Clear();

            _disposed = true;
            _logger.LogDebug("ServerMetaService disposed");
        }
    }
}