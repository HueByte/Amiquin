using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Sleep;

/// <summary>
/// Service implementation for managing bot sleep functionality per server.
/// </summary>
public class SleepService : ISleepService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SleepService> _logger;
    private const string SleepKeyPrefix = "sleep_";

    public SleepService(IMemoryCache memoryCache, ILogger<SleepService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<DateTime> PutToSleepAsync(ulong serverId, int durationMinutes)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive", nameof(durationMinutes));

        if (durationMinutes > 24 * 60) // 24 hours max
            throw new ArgumentException("Duration cannot exceed 24 hours", nameof(durationMinutes));

        var wakeUpTime = DateTime.UtcNow.AddMinutes(durationMinutes);
        var cacheKey = $"{SleepKeyPrefix}{serverId}";

        _memoryCache.Set(cacheKey, wakeUpTime, wakeUpTime);
        
        _logger.LogInformation("Bot put to sleep on server {ServerId} for {DurationMinutes} minutes until {WakeUpTime}", 
            serverId, durationMinutes, wakeUpTime);

        return Task.FromResult(wakeUpTime);
    }

    /// <inheritdoc/>
    public Task<bool> IsSleepingAsync(ulong serverId)
    {
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        
        if (_memoryCache.TryGetValue(cacheKey, out DateTime wakeUpTime))
        {
            if (DateTime.UtcNow < wakeUpTime)
            {
                return Task.FromResult(true); // Still sleeping
            }
            else
            {
                // Sleep expired, remove from cache
                _memoryCache.Remove(cacheKey);
                _logger.LogDebug("Sleep expired for server {ServerId}, automatically woke up", serverId);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false); // Not sleeping
    }

    /// <inheritdoc/>
    public Task<TimeSpan?> GetRemainingSleepTimeAsync(ulong serverId)
    {
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        
        if (_memoryCache.TryGetValue(cacheKey, out DateTime wakeUpTime))
        {
            var remaining = wakeUpTime - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return Task.FromResult<TimeSpan?>(remaining);
            }
            else
            {
                // Sleep expired, remove from cache
                _memoryCache.Remove(cacheKey);
                return Task.FromResult<TimeSpan?>(null);
            }
        }

        return Task.FromResult<TimeSpan?>(null); // Not sleeping
    }

    /// <inheritdoc/>
    public Task<bool> WakeUpAsync(ulong serverId)
    {
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        
        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            _memoryCache.Remove(cacheKey);
            _logger.LogInformation("Bot manually woken up on server {ServerId}", serverId);
            return Task.FromResult(true); // Was sleeping, now awake
        }

        return Task.FromResult(false); // Was already awake
    }

    /// <inheritdoc/>
    public Task<Dictionary<ulong, DateTime>> GetSleepingServersAsync()
    {
        // This is a simplified implementation. In a production environment,
        // you might want to use a more sophisticated caching solution that
        // allows enumeration of keys with a specific prefix.
        
        // For now, return empty dictionary as MemoryCache doesn't easily support enumeration
        // This could be enhanced by using a separate dictionary to track sleeping servers
        return Task.FromResult(new Dictionary<ulong, DateTime>());
    }
}