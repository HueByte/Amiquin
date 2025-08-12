using Amiquin.Core.Abstraction;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Job.Models;
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

            // Get all servers from database - this should be fast
            List<ulong> serverIds;
            using (var scope = serviceScopeFactory.CreateScope())
            {
                var serverRepository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
                serverIds = serverRepository.AsQueryable().Select(s => s.Id).ToList();
            }
            _logger.LogDebug("Found {Count} servers to check for activity sessions", serverIds.Count);

            var serversProcessed = 0;
            var sessionsCreated = 0;
            var sessionsRemoved = 0;

            // Process servers in batches to avoid timeout issues
            const int batchSize = 10;
            for (int i = 0; i < serverIds.Count; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var batch = serverIds.Skip(i).Take(batchSize).ToList();

                // Process batch in parallel for efficiency
                var tasks = batch.Select(async serverId =>
                {
                    try
                    {
                        // Check toggle status with timeout - each task gets its own scope to prevent DbContext concurrency issues
                        bool isLiveJobEnabled = false;
                        try
                        {
                            using var toggleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                            toggleCts.CancelAfter(TimeSpan.FromSeconds(2)); // 2 second timeout per toggle check

                            // Create a separate scope for this parallel task to avoid DbContext concurrency issues
                            using var toggleScope = serviceScopeFactory.CreateScope();
                            var toggleService = toggleScope.ServiceProvider.GetRequiredService<IToggleService>();
                            isLiveJobEnabled = await toggleService.IsEnabledAsync(serverId, Constants.ToggleNames.EnableLiveJob);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error checking toggle for server {ServerId}, assuming disabled", serverId);
                        }

                        var currentlyHasSession = _activeSessionJobs.ContainsKey(serverId);

                        if (isLiveJobEnabled && !currentlyHasSession)
                        {
                            // Thread-safe check to prevent duplicate job creation during parallel processing
                            var jobId = $"ActivitySession_{serverId}";

                            // Use TryAdd to atomically add the job ID only if it doesn't exist
                            // This prevents race conditions when multiple threads try to create the same job
                            if (_activeSessionJobs.TryAdd(serverId, jobId))
                            {
                                // Only proceed if we successfully added the job ID to the dictionary
                                var sessionJobId = CreateActivitySessionJob(serverId, serviceScopeFactory);
                                if (!string.IsNullOrEmpty(sessionJobId))
                                {
                                    Interlocked.Increment(ref sessionsCreated);
                                    _logger.LogInformation("Created ActivitySession for guild {GuildId}", serverId);
                                }
                                else
                                {
                                    // If job creation failed, remove from dictionary to allow retry
                                    _activeSessionJobs.TryRemove(serverId, out _);
                                    _logger.LogError("Failed to create ActivitySession job for guild {GuildId}, removed from tracking", serverId);
                                }
                            }
                            else
                            {
                                _logger.LogDebug("ActivitySession job for guild {GuildId} is already being created by another thread", serverId);
                            }
                        }
                        else if (!isLiveJobEnabled && currentlyHasSession)
                        {
                            // Remove activity session for this server
                            if (_activeSessionJobs.TryRemove(serverId, out var jobId))
                            {
                                _jobService.CancelJob(jobId);
                                Interlocked.Increment(ref sessionsRemoved);
                                _logger.LogInformation("Removed ActivitySession for guild {GuildId}", serverId);
                            }
                        }

                        Interlocked.Increment(ref serversProcessed);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing server {ServerId} for activity session management", serverId);
                    }
                });

                // Wait for batch to complete with overall timeout
                try
                {
                    using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    batchCts.CancelAfter(TimeSpan.FromSeconds(15)); // 15 seconds max per batch

                    await Task.WhenAll(tasks).WaitAsync(batchCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Batch processing timed out or was cancelled");
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        // If it was a timeout (not general cancellation), continue to next batch
                        continue;
                    }
                    break;
                }
            }

            // Cleanup orphaned jobs - remove from tracking any jobs that no longer exist in JobService
            await CleanupOrphanedJobsAsync();

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

            // Create a simple AmiquinJob that uses the existing ActivitySessionJob class
            var amiquinJob = new AmiquinJob
            {
                Id = jobId,
                Name = "ActivitySession",
                Interval = TimeSpan.FromSeconds(6), // Start with faster frequency
                Task = async (scopeFactory, cancellationToken) =>
                {
                    // Create ActivitySessionJob instance and execute it
                    using var scope = scopeFactory.CreateScope();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ActivitySessionJob>>();
                    var activitySessionJob = new ActivitySessionJob(serverId, logger);
                    await activitySessionJob.ExecuteAsync(scopeFactory, cancellationToken);
                }
            };

            // Register the job with JobService
            var isSuccess = _jobService.CreateDynamicJob(amiquinJob);

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
    /// Cleans up orphaned jobs that are tracked in the dictionary but no longer exist in JobService
    /// </summary>
    private Task CleanupOrphanedJobsAsync()
    {
        try
        {
            var orphanedJobs = new List<ulong>();

            foreach (var kvp in _activeSessionJobs)
            {
                var serverId = kvp.Key;
                var jobId = kvp.Value;

                // Check if the job actually exists in JobService
                if (!_jobService.JobExists(jobId))
                {
                    orphanedJobs.Add(serverId);
                    _logger.LogWarning("Found orphaned ActivitySession tracking for guild {GuildId} (job {JobId} doesn't exist)", serverId, jobId);
                }
            }

            // Remove orphaned entries
            foreach (var serverId in orphanedJobs)
            {
                if (_activeSessionJobs.TryRemove(serverId, out var jobId))
                {
                    _logger.LogInformation("Cleaned up orphaned ActivitySession tracking for guild {GuildId}", serverId);
                }
            }

            if (orphanedJobs.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} orphaned ActivitySession entries", orphanedJobs.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during orphaned job cleanup");
        }

        return Task.CompletedTask;
    }

}