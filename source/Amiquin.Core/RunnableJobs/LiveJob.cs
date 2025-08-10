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

            // Create the activity session job using the AmiquinJob model
            var amiquinJob = new AmiquinJob
            {
                Id = jobId,
                Name = $"ActivitySession",
                Description = $"Activity session job for guild {serverId}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GuildId = serverId,
                Interval = TimeSpan.FromSeconds(8), // Start with fast frequency, will be adjusted by ActivitySessionJob
                Task = async (scopeFactory, cancellationToken) =>
                {
                    // Create ActivitySessionJob instance and execute it
                    using var scope = scopeFactory.CreateScope();
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ActivitySessionJob>>();
                    var activitySession = new ActivitySessionJob(serverId, logger);
                    await activitySession.ExecuteAsync(scopeFactory, cancellationToken);
                }
            };

            // Register the dynamic job with JobService
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