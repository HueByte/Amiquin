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

            using var scope = serviceScopeFactory.CreateScope();
            var serverRepository = scope.ServiceProvider.GetRequiredService<IServerMetaRepository>();
            var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();

            // Get all servers from database - this should be fast
            var serverIds = serverRepository.AsQueryable().Select(s => s.Id).ToList();
            _logger.LogDebug("Found {Count} servers to check for activity sessions", serverIds.Count);

            var serversProcessed = 0;
            var sessionsCreated = 0;
            var sessionsRemoved = 0;

            // Check toggle status for all servers at once to avoid potential hanging
            var serverToggleStatus = new Dictionary<ulong, bool>();
            foreach (var serverId in serverIds)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    // Set a reasonable timeout for toggle checks
                    using var toggleCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    toggleCts.CancelAfter(TimeSpan.FromSeconds(5)); // 5 second timeout per toggle check
                    
                    var isEnabled = await toggleService.IsEnabledAsync(serverId, Constants.ToggleNames.EnableLiveJob);
                    serverToggleStatus[serverId] = isEnabled;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Toggle check timed out for server {ServerId}, assuming disabled", serverId);
                    serverToggleStatus[serverId] = false;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking toggle for server {ServerId}, assuming disabled", serverId);
                    serverToggleStatus[serverId] = false;
                }
            }

            // Process each server quickly using cached toggle status
            foreach (var serverId in serverIds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var isLiveJobEnabled = serverToggleStatus.GetValueOrDefault(serverId, false);
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


}