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
    public int CurrentFrequencySeconds { get; private set; } = 8; // Start with fast frequency

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

            // Execute activity session with frequency adjustment callback
            var wasEngaged = await activitySessionService.ExecuteActivitySessionAsync(_guildId, activityLevel =>
            {
                LastActivityLevel = activityLevel;
                AdjustFrequency(activityLevel);
            });

            // Log the result (already logged inside service, but keeping for consistency with old behavior)
            if (!wasEngaged)
            {
                _logger.LogDebug("No engagement action was executed for guild {GuildId}", _guildId);
            }
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
            <= 0.1 => 30,       // Very low activity: every 30 seconds
            <= 0.3 => 20,       // Low activity: every 20 seconds  
            <= 0.7 => 15,       // Normal activity: every 15 seconds
            <= 1.3 => 10,       // High activity: every 10 seconds
            <= 1.5 => 8,        // Very high activity: every 8 seconds
            _ => 6              // Extreme activity: every 6 seconds
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