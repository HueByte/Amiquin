using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Job.Models;
using Amiquin.Core.Services.ActivitySession;
using Amiquin.Core.Services.Toggle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.RunnableJobs;

/// <summary>
/// LiveJob Coordinator - Creates and manages individual ActivitySessionJob instances per server.
/// This job runs periodically to ensure each active server has its own activity session job.
/// </summary>
public class LiveJob : IRunnableJob
{
    private readonly ILogger<LiveJob> _logger;
    private readonly IJobService _jobService;
    private readonly ConcurrentDictionary<ulong, string> _activeSessionJobs = new(); // serverId -> jobId

    public LiveJob(ILogger<LiveJob> logger, IJobService jobService)
    {
        _logger = logger;
        _jobService = jobService;
    }

    public int FrequencyInSeconds { get; set; } = 60; // Coordinator runs every minute to manage sessions

    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("LiveJob Coordinator starting - managing activity session jobs");

            using var scope = serviceScopeFactory.CreateScope();
            var serverRepository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
            var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();

            // Get all servers from database - this should be fast
            var serverIds = serverRepository.AsQueryable().Select(s => s.Id).ToList();
            _logger.LogDebug("Found {Count} servers to check for activity sessions", serverIds.Count);

            var serversProcessed = 0;
            var sessionsCreated = 0;
            var sessionsRemoved = 0;

            // Process each server quickly
            foreach (var serverId in serverIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var isLiveJobEnabled = await toggleService.IsEnabledAsync(serverId, Constants.ToggleNames.EnableLiveJob);
                    var currentlyHasSession = _activeSessionJobs.ContainsKey(serverId);

                    if (isLiveJobEnabled && !currentlyHasSession)
                    {
                        // Create new activity session for this server
                        var sessionJobId = CreateActivitySessionJob(serverId, serviceScopeFactory);
                        if (!string.IsNullOrEmpty(sessionJobId))
                        {
                            _activeSessionJobs[serverId] = sessionJobId;
                            sessionsCreated++;
                            _logger.LogInformation("Created ActivitySession for guild {GuildId}", serverId);
                        }
                    }
                    else if (!isLiveJobEnabled && currentlyHasSession)
                    {
                        // Remove activity session for this server
                        if (_activeSessionJobs.TryRemove(serverId, out var jobId))
                        {
                            _jobService.CancelJob(jobId);
                            sessionsRemoved++;
                            _logger.LogInformation("Removed ActivitySession for guild {GuildId}", serverId);
                        }
                    }

                    serversProcessed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing server {ServerId} for activity session management", serverId);
                }
            }

            _logger.LogInformation("LiveJob Coordinator completed: {Processed} servers, {Created} sessions created, {Removed} sessions removed",
                serversProcessed, sessionsCreated, sessionsRemoved);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LiveJob Coordinator execution");
            throw; // Re-throw to trigger retry logic in JobService
        }
    }

    /// <summary>
    /// Creates a new ActivitySessionJob for a specific server
    /// </summary>
    private string? CreateActivitySessionJob(ulong serverId, IServiceScopeFactory serviceScopeFactory)
    {
        try
        {
            var jobId = $"ActivitySession_{serverId}";
            
            // Create a tracked job that handles activity session for this server
            var trackedJob = new TrackedAmiquinJob
            {
                Id = jobId,
                Name = "ActivitySession",
                Description = $"Activity session job for guild {serverId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GuildId = serverId,
                Interval = TimeSpan.FromSeconds(8), // Start with fast frequency
                Task = async (scopeFactory, job, cancellationToken) =>
                {
                    // Execute the activity session logic directly
                    await ExecuteActivitySessionAsync(scopeFactory, job, cancellationToken);
                },
                AutoRestart = true,
                MaxRetryAttempts = 3
            };

            // Register the tracked job with JobService
            var isSuccess = _jobService.CreateTrackedJob(trackedJob);
            
            if (isSuccess)
            {
                _logger.LogInformation("Successfully created ActivitySession job for guild {GuildId}", serverId);
                return jobId;
            }
            else
            {
                _logger.LogError("Failed to create ActivitySession job for guild {GuildId}", serverId);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ActivitySession job for guild {GuildId}", serverId);
            return null;
        }
    }

    /// <summary>
    /// Executes the activity session logic for a specific server
    /// </summary>
    private async Task ExecuteActivitySessionAsync(IServiceScopeFactory serviceScopeFactory, TrackedAmiquinJob job, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Executing ActivitySession for guild {GuildId} (execution #{Count})", job.GuildId, job.ExecutionCount);

            using var scope = serviceScopeFactory.CreateScope();
            var activitySessionService = scope.ServiceProvider.GetRequiredService<IActivitySessionService>();

            // Execute activity session with frequency adjustment callback
            await activitySessionService.ExecuteActivitySessionAsync(job.GuildId, activityLevel =>
            {
                AdjustJobFrequency(job, activityLevel);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ActivitySession for guild {GuildId}", job.GuildId);
            throw; // Re-throw to let JobService handle retries
        }
    }

    /// <summary>
    /// Adjusts the job's execution frequency based on activity level
    /// </summary>
    private void AdjustJobFrequency(TrackedAmiquinJob job, double activityLevel)
    {
        var newFrequencySeconds = activityLevel switch
        {
            <= 0.1 => 30,       // Very low activity: every 30 seconds
            <= 0.3 => 20,       // Low activity: every 20 seconds  
            <= 0.7 => 15,       // Normal activity: every 15 seconds
            <= 1.3 => 10,       // High activity: every 10 seconds
            <= 1.5 => 8,        // Very high activity: every 8 seconds
            _ => 6              // Extreme activity: every 6 seconds
        };

        var newInterval = TimeSpan.FromSeconds(newFrequencySeconds);
        if (job.Interval != newInterval)
        {
            var oldInterval = job.Interval;
            job.Interval = newInterval;
            job.UpdatedAt = DateTime.UtcNow;
            _logger.LogDebug("Adjusted frequency for guild {GuildId}: {OldFreq}s → {NewFreq}s (activity: {Activity})", 
                job.GuildId, oldInterval.TotalSeconds, newFrequencySeconds, activityLevel);
        }
    }

}