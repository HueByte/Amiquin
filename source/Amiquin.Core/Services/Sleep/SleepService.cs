using Amiquin.Core.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Sleep;

/// <summary>
/// Service implementation for managing bot sleep functionality per server.
/// Supports both manual sleep (user-triggered) and deep sleep (inactivity-triggered).
/// </summary>
public class SleepService : ISleepService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SleepService> _logger;
    private readonly InitiativeOptions _initiativeOptions;

    private const string SleepKeyPrefix = "sleep_";

    // Manual sleep tracking
    private readonly ConcurrentDictionary<ulong, DateTime> _sleepingServers = new();

    // Activity and initiative state tracking
    private readonly ConcurrentDictionary<ulong, ServerActivityState> _activityStates = new();

    public SleepService(
        IMemoryCache memoryCache,
        ILogger<SleepService> logger,
        IOptions<InitiativeOptions> initiativeOptions)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _initiativeOptions = initiativeOptions.Value;
    }

    #region Manual Sleep Methods

    /// <inheritdoc/>
    public Task<DateTime> PutToSleepAsync(ulong serverId, int durationMinutes)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive", nameof(durationMinutes));

        if (durationMinutes > 24 * 60) // 24 hours max
            throw new ArgumentException("Duration cannot exceed 24 hours", nameof(durationMinutes));

        var wakeUpTime = DateTime.UtcNow.AddMinutes(durationMinutes);
        var cacheKey = $"{SleepKeyPrefix}{serverId}";

        _sleepingServers[serverId] = wakeUpTime;

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = wakeUpTime
        };
        options.RegisterPostEvictionCallback((key, _, _, _) =>
        {
            _sleepingServers.TryRemove(serverId, out _);
        });

        _memoryCache.Set(cacheKey, wakeUpTime, options);

        _logger.LogInformation("Bot put to sleep on server {ServerId} for {DurationMinutes} minutes until {WakeUpTime}",
            serverId, durationMinutes, wakeUpTime);

        return Task.FromResult(wakeUpTime);
    }

    /// <inheritdoc/>
    public async Task<bool> IsSleepingAsync(ulong serverId)
    {
        // Check manual sleep first
        var cacheKey = $"{SleepKeyPrefix}{serverId}";

        if (_memoryCache.TryGetValue(cacheKey, out DateTime wakeUpTime))
        {
            if (DateTime.UtcNow < wakeUpTime)
            {
                return true; // Still in manual sleep
            }
            else
            {
                // Sleep expired, remove from cache
                _memoryCache.Remove(cacheKey);
                _sleepingServers.TryRemove(serverId, out _);
                _logger.LogDebug("Manual sleep expired for server {ServerId}, automatically woke up", serverId);
            }
        }

        // Check deep sleep
        return await IsInDeepSleepAsync(serverId);
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
                _sleepingServers.TryRemove(serverId, out _);
                return Task.FromResult<TimeSpan?>(null);
            }
        }

        return Task.FromResult<TimeSpan?>(null); // Not in manual sleep
    }

    /// <inheritdoc/>
    public Task<bool> WakeUpAsync(ulong serverId)
    {
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        var wasManualSleeping = false;

        if (_memoryCache.TryGetValue(cacheKey, out _))
        {
            _memoryCache.Remove(cacheKey);
            _sleepingServers.TryRemove(serverId, out _);
            wasManualSleeping = true;
            _logger.LogInformation("Bot manually woken up from manual sleep on server {ServerId}", serverId);
        }

        // Also reset deep sleep state
        if (_activityStates.TryGetValue(serverId, out var state))
        {
            var wasDeepSleeping = state.IsInDeepSleep;
            state.IsInDeepSleep = false;
            state.WakeUpStarted = DateTime.UtcNow;
            state.ResetMessagesSinceWakeUp(); // Atomic reset

            if (wasDeepSleeping)
            {
                _logger.LogInformation("Bot manually woken up from deep sleep on server {ServerId}", serverId);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(wasManualSleeping);
    }

    /// <inheritdoc/>
    public Task<Dictionary<ulong, DateTime>> GetSleepingServersAsync()
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _sleepingServers)
        {
            if (kvp.Value <= now)
            {
                _sleepingServers.TryRemove(kvp.Key, out _);
            }
        }

        return Task.FromResult(_sleepingServers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    #endregion

    #region Deep Sleep Methods

    /// <inheritdoc/>
    public Task<bool> IsInDeepSleepAsync(ulong serverId)
    {
        if (!_initiativeOptions.DeepSleep.Enabled)
        {
            return Task.FromResult(false);
        }

        var state = GetOrCreateActivityState(serverId);
        var now = DateTime.UtcNow;

        // Check if we have any activity recorded
        if (!state.LastActivity.HasValue)
        {
            // No activity ever recorded - don't put into deep sleep immediately
            // This handles new servers or bot restarts
            return Task.FromResult(false);
        }

        var hoursSinceActivity = (now - state.LastActivity.Value).TotalHours;
        var threshold = _initiativeOptions.DeepSleep.InactivityHoursThreshold;

        if (hoursSinceActivity >= threshold)
        {
            if (!state.IsInDeepSleep)
            {
                state.IsInDeepSleep = true;
                state.DeepSleepStarted = now;
                _logger.LogInformation("Server {ServerId} entered deep sleep after {Hours:F1} hours of inactivity",
                    serverId, hoursSinceActivity);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(state.IsInDeepSleep);
    }

    /// <inheritdoc/>
    public Task RecordActivityAsync(ulong serverId)
    {
        var state = GetOrCreateActivityState(serverId);
        var now = DateTime.UtcNow;
        var wasInDeepSleep = state.IsInDeepSleep;

        state.LastActivity = now;
        state.ResetConsecutiveInitiatives(); // Reset consecutive count on user activity (atomic)

        if (wasInDeepSleep)
        {
            // Use atomic increment to handle concurrent activity recording
            var newMessageCount = state.IncrementMessagesSinceWakeUp();
            var wakeUpThreshold = _initiativeOptions.DeepSleep.WakeUpMessageThreshold;

            if (newMessageCount >= wakeUpThreshold)
            {
                state.IsInDeepSleep = false;
                state.WakeUpStarted = now;
                _logger.LogInformation("Server {ServerId} waking up from deep sleep after {Messages} messages",
                    serverId, newMessageCount);
            }
            else
            {
                _logger.LogDebug("Server {ServerId} received activity during deep sleep ({Messages}/{Threshold} messages)",
                    serverId, newMessageCount, wakeUpThreshold);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DateTime?> GetLastActivityAsync(ulong serverId)
    {
        if (_activityStates.TryGetValue(serverId, out var state))
        {
            return Task.FromResult(state.LastActivity);
        }

        return Task.FromResult<DateTime?>(null);
    }

    /// <inheritdoc/>
    public async Task<InitiativeState> GetInitiativeStateAsync(ulong serverId)
    {
        var state = GetOrCreateActivityState(serverId);
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        var isManualSleeping = _memoryCache.TryGetValue(cacheKey, out DateTime wakeUpTime) && DateTime.UtcNow < wakeUpTime;
        var isInDeepSleep = await IsInDeepSleepAsync(serverId);

        // Determine if in gradual wake-up period
        var isWakingUp = false;
        if (!isInDeepSleep && state.WakeUpStarted.HasValue)
        {
            var hoursSinceWakeUp = (DateTime.UtcNow - state.WakeUpStarted.Value).TotalHours;
            isWakingUp = hoursSinceWakeUp < _initiativeOptions.DeepSleep.GradualWakeUpHours;
        }

        return new InitiativeState
        {
            IsInDeepSleep = isInDeepSleep,
            IsInManualSleep = isManualSleeping,
            IsWakingUp = isWakingUp,
            LastActivity = state.LastActivity,
            LastInitiativeAction = state.LastInitiativeAction,
            ConsecutiveInitiatives = state.ConsecutiveInitiatives,
            MessagesSinceWakeUp = state.MessagesSinceWakeUp,
            ProbabilityMultiplier = await GetInitiativeProbabilityMultiplierAsync(serverId),
            DeepSleepStarted = state.DeepSleepStarted,
            WakeUpStarted = state.WakeUpStarted
        };
    }

    /// <inheritdoc/>
    public Task RecordInitiativeActionAsync(ulong serverId)
    {
        var state = GetOrCreateActivityState(serverId);
        state.LastInitiativeAction = DateTime.UtcNow;
        var newCount = state.IncrementConsecutiveInitiatives(); // Atomic increment

        _logger.LogDebug("Recorded initiative action for server {ServerId}, consecutive count: {Count}",
            serverId, newCount);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<float> GetInitiativeProbabilityMultiplierAsync(ulong serverId)
    {
        if (!_initiativeOptions.Enabled)
        {
            return 0f;
        }

        var state = GetOrCreateActivityState(serverId);
        var multiplier = 1.0f;

        // Check manual sleep
        var cacheKey = $"{SleepKeyPrefix}{serverId}";
        if (_memoryCache.TryGetValue(cacheKey, out DateTime wakeUpTime) && DateTime.UtcNow < wakeUpTime)
        {
            return 0f; // No initiative during manual sleep
        }

        // Check deep sleep
        if (await IsInDeepSleepAsync(serverId))
        {
            return 0f; // No initiative during deep sleep
        }

        // Check gradual wake-up period
        if (state.WakeUpStarted.HasValue)
        {
            var hoursSinceWakeUp = (DateTime.UtcNow - state.WakeUpStarted.Value).TotalHours;
            if (hoursSinceWakeUp < _initiativeOptions.DeepSleep.GradualWakeUpHours)
            {
                multiplier *= _initiativeOptions.DeepSleep.WakeUpProbabilityMultiplier;
                _logger.LogDebug("Server {ServerId} in gradual wake-up, multiplier: {Multiplier}", serverId, multiplier);
            }
        }

        // Check consecutive initiatives - use gradual decay instead of hard cutoff
        // This makes the bot feel more natural by allowing occasional follow-ups
        // but with exponentially decreasing probability
        var consecutiveCount = state.ConsecutiveInitiatives;
        if (consecutiveCount > 0)
        {
            var maxConsecutive = _initiativeOptions.Engagement.MaxConsecutiveInitiatives;
            var reductionFactor = _initiativeOptions.Engagement.ConsecutiveReductionFactor;

            // Calculate exponential decay: factor^consecutive_count
            // e.g., with factor=0.5: 1st=0.5, 2nd=0.25, 3rd=0.125, etc.
            var decayMultiplier = (float)Math.Pow(reductionFactor, consecutiveCount);

            // After max consecutive, apply additional penalty but don't go to zero
            // This allows rare engagement even after max, simulating human "one more thing"
            if (consecutiveCount >= maxConsecutive)
            {
                // Apply extra penalty: multiply by 0.1 after max, making it very rare but not impossible
                decayMultiplier *= 0.1f;
                _logger.LogDebug("Server {ServerId} at/past max consecutive initiatives ({Count}/{Max}), multiplier: {Multiplier}",
                    serverId, consecutiveCount, maxConsecutive, decayMultiplier);
            }
            else
            {
                _logger.LogDebug("Server {ServerId} has {Count} consecutive initiatives, decay multiplier: {Multiplier}",
                    serverId, consecutiveCount, decayMultiplier);
            }

            multiplier *= decayMultiplier;
        }

        // Check minimum time between initiatives
        if (state.LastInitiativeAction.HasValue)
        {
            var minutesSinceLastInitiative = (DateTime.UtcNow - state.LastInitiativeAction.Value).TotalMinutes;
            if (minutesSinceLastInitiative < _initiativeOptions.Timing.MinMinutesBetweenInitiatives)
            {
                multiplier = 0f;
                _logger.LogDebug("Server {ServerId} too soon since last initiative ({Minutes:F1} min < {Min} min)",
                    serverId, minutesSinceLastInitiative, _initiativeOptions.Timing.MinMinutesBetweenInitiatives);
            }
        }

        // Check active hours
        if (_initiativeOptions.Timing.ActiveHours.Enabled)
        {
            var currentHour = DateTime.Now.Hour; // Use local time
            var startHour = _initiativeOptions.Timing.ActiveHours.StartHour;
            var endHour = _initiativeOptions.Timing.ActiveHours.EndHour;

            bool isActiveHour;
            if (startHour <= endHour)
            {
                isActiveHour = currentHour >= startHour && currentHour < endHour;
            }
            else
            {
                // Handles overnight ranges like 22-6
                isActiveHour = currentHour >= startHour || currentHour < endHour;
            }

            if (!isActiveHour)
            {
                multiplier *= _initiativeOptions.Timing.ActiveHours.InactiveHoursMultiplier;
            }
        }

        return Math.Max(0f, Math.Min(1f, multiplier));
    }

    #endregion

    #region Private Helpers

    private ServerActivityState GetOrCreateActivityState(ulong serverId)
    {
        return _activityStates.GetOrAdd(serverId, _ => new ServerActivityState
        {
            LastActivity = DateTime.UtcNow // Initialize with current time for new servers
        });
    }

    #endregion
}

/// <summary>
/// Internal state tracking for server activity and initiative.
/// Uses thread-safe counters for concurrent access.
/// </summary>
internal class ServerActivityState
{
    public DateTime? LastActivity { get; set; }
    public DateTime? LastInitiativeAction { get; set; }
    public bool IsInDeepSleep { get; set; }
    public DateTime? DeepSleepStarted { get; set; }
    public DateTime? WakeUpStarted { get; set; }

    // Thread-safe counters
    private int _consecutiveInitiatives;
    private int _messagesSinceWakeUp;

    public int ConsecutiveInitiatives
    {
        get => Interlocked.CompareExchange(ref _consecutiveInitiatives, 0, 0);
        set => Interlocked.Exchange(ref _consecutiveInitiatives, value);
    }

    public int MessagesSinceWakeUp
    {
        get => Interlocked.CompareExchange(ref _messagesSinceWakeUp, 0, 0);
        set => Interlocked.Exchange(ref _messagesSinceWakeUp, value);
    }

    /// <summary>
    /// Atomically increments MessagesSinceWakeUp and returns the new value.
    /// </summary>
    public int IncrementMessagesSinceWakeUp() => Interlocked.Increment(ref _messagesSinceWakeUp);

    /// <summary>
    /// Atomically increments ConsecutiveInitiatives and returns the new value.
    /// </summary>
    public int IncrementConsecutiveInitiatives() => Interlocked.Increment(ref _consecutiveInitiatives);

    /// <summary>
    /// Atomically resets ConsecutiveInitiatives to zero.
    /// </summary>
    public void ResetConsecutiveInitiatives() => Interlocked.Exchange(ref _consecutiveInitiatives, 0);

    /// <summary>
    /// Atomically resets MessagesSinceWakeUp to zero.
    /// </summary>
    public void ResetMessagesSinceWakeUp() => Interlocked.Exchange(ref _messagesSinceWakeUp, 0);
}
