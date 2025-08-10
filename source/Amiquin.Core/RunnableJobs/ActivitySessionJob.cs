using Amiquin.Core.Services.ActivitySession;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.RunnableJobs;

/// <summary>
/// Individual server activity session job that handles engagement for a specific server.
/// This job adapts its behavior based on real-time activity levels.
/// </summary>
public class ActivitySessionJob
{
    private readonly ulong _guildId;
    private readonly ILogger<ActivitySessionJob> _logger;
    
    public ulong GuildId => _guildId;
    public string JobId => $"ActivitySession_{_guildId}";
    public DateTime CreatedAt { get; }
    public DateTime LastExecutedAt { get; private set; }
    public int ExecutionCount { get; private set; }
    public double LastActivityLevel { get; private set; }
    public int CurrentFrequencySeconds { get; private set; } = 6; // Start with faster frequency

    public ActivitySessionJob(ulong guildId, ILogger<ActivitySessionJob> logger)
    {
        _guildId = guildId;
        _logger = logger;
        CreatedAt = DateTime.UtcNow;
        LastExecutedAt = DateTime.MinValue;
    }

    /// <summary>
    /// Executes the activity session logic for this specific server
    /// </summary>
    public async Task ExecuteAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionCount++;
            LastExecutedAt = DateTime.UtcNow;
            
            _logger.LogDebug("Executing ActivitySession for guild {GuildId} (execution #{Count})", _guildId, ExecutionCount);

            using var scope = serviceScopeFactory.CreateScope();
            var activitySessionService = scope.ServiceProvider.GetRequiredService<IActivitySessionService>();

            // Create a timeout for the execution to prevent TaskManager timeouts
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30)); // 30-second timeout per execution

            // Execute activity session with frequency adjustment callback
            var wasEngaged = await activitySessionService.ExecuteActivitySessionAsync(_guildId, activityLevel =>
            {
                LastActivityLevel = activityLevel;
                AdjustFrequency(activityLevel);
            }, timeoutCts.Token);

            // Log the result (already logged inside service, but keeping for consistency with old behavior)
            if (!wasEngaged)
            {
                _logger.LogDebug("No engagement action was executed for guild {GuildId}", _guildId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("ActivitySession execution cancelled for guild {GuildId}", _guildId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ActivitySession execution timed out for guild {GuildId} after 30 seconds", _guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ActivitySession for guild {GuildId}", _guildId);
        }
    }

    /// <summary>
    /// Adjusts execution frequency based on activity level
    /// </summary>
    private void AdjustFrequency(double activityLevel)
    {
        var newFrequency = activityLevel switch
        {
            <= 0.1 => 15,       // Very low activity: every 15 seconds (reduced from 30s)
            <= 0.3 => 12,       // Low activity: every 12 seconds (reduced from 20s)
            <= 0.7 => 8,        // Normal activity: every 8 seconds (reduced from 15s)
            <= 1.3 => 6,        // High activity: every 6 seconds (reduced from 10s)
            <= 1.5 => 5,        // Very high activity: every 5 seconds (reduced from 8s)
            _ => 4              // Extreme activity: every 4 seconds (reduced from 6s)
        };

        if (newFrequency != CurrentFrequencySeconds)
        {
            var oldFrequency = CurrentFrequencySeconds;
            CurrentFrequencySeconds = newFrequency;
            _logger.LogDebug("Adjusted frequency for guild {GuildId}: {OldFreq}s → {NewFreq}s (activity: {Activity})", 
                _guildId, oldFrequency, newFrequency, activityLevel);
        }
    }

}