using Amiquin.Core.Abstraction;
using Amiquin.Core.Job.Models;
using Amiquin.Core.Options;
using Jiro.Shared.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Amiquin.Core.Job;

/// <summary>
/// Advanced job service implementation using Jiro.Shared TaskManager for improved
/// resource management, health monitoring, and fault tolerance.
/// </summary>
public class JobService : IAsyncDisposable, IJobService
{
    private readonly ILogger<JobService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITaskManager _taskManager;
    private readonly JobManagerOptions _options;
    private readonly ConcurrentDictionary<string, TrackedAmiquinJob> _dynamicJobs = new();
    private readonly ConcurrentDictionary<string, Timer> _jobTimers = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellationTokens = new();
    private readonly SemaphoreSlim _jobManagementLock = new(1, 1);
    private volatile bool _disposed;

    /// <summary>
    /// Gets the current number of active jobs.
    /// </summary>
    public int ActiveJobCount => _dynamicJobs.Count;

    /// <summary>
    /// Gets the collection of all registered jobs.
    /// </summary>
    public IReadOnlyDictionary<string, TrackedAmiquinJob> Jobs => _dynamicJobs.AsReadOnly();

    public JobService(
        ILogger<JobService> logger, 
        IServiceScopeFactory serviceScopeFactory, 
        ITaskManager taskManager,
        IOptions<JobManagerOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        _logger.LogInformation("JobService initialized with TaskManager integration");
    }

    /// <summary>
    /// Starts all runnable jobs registered in the dependency injection container.
    /// </summary>
    public void StartRunnableJobs()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot start runnable jobs - JobService is disposed");
            return;
        }

        _logger.LogInformation("Starting runnable jobs with TaskManager");

        using var scope = _serviceScopeFactory.CreateScope();
        var runnableJobs = scope.ServiceProvider.GetServices<IRunnableJob>();
        var registeredCount = 0;

        foreach (var runnableJob in runnableJobs)
        {
            var trackedJob = new TrackedAmiquinJob
            {
                Id = Guid.NewGuid().ToString(),
                Name = runnableJob.GetType().Name,
                Description = $"Runnable job: {runnableJob.GetType().Name}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GuildId = 0,
                Task = runnableJob.RunAsync,
                Interval = ValidateJobInterval(TimeSpan.FromSeconds(runnableJob.FrequencyInSeconds)),
                Status = JobStatus.Pending,
                AutoRestart = _options.EnableAutoRestart,
                MaxRetryAttempts = _options.DefaultMaxRetryAttempts
            };

            if (RegisterJobInternal(trackedJob))
            {
                registeredCount++;
            }
        }

        _logger.LogInformation("Successfully registered {Count} runnable jobs", registeredCount);
    }

    /// <summary>
    /// Creates a new dynamic job with enhanced tracking and management capabilities.
    /// </summary>
    /// <param name="job">The legacy AmiquinJob to convert and register.</param>
    /// <returns>True if the job was successfully created; otherwise, false.</returns>
    public bool CreateDynamicJob(AmiquinJob job)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot create dynamic job - JobService is disposed");
            return false;
        }

        _logger.LogInformation("Creating dynamic job {JobName} [{JobId}]", job.Name, job.Id);
        
        if (string.IsNullOrEmpty(job.Id) || job.Task is null)
        {
            _logger.LogError("Job {JobName} does not have a valid task or ID", job.Name);
            return false;
        }

        if (_dynamicJobs.ContainsKey(job.Id))
        {
            _logger.LogWarning("Job {JobName} [{JobId}] already exists", job.Name, job.Id);
            return false;
        }

        // Convert legacy AmiquinJob to TrackedAmiquinJob
        var trackedJob = new TrackedAmiquinJob
        {
            Id = job.Id,
            Name = job.Name,
            Description = job.Description,
            CreatedAt = job.CreatedAt,
            UpdatedAt = job.UpdatedAt,
            GuildId = job.GuildId,
            Task = job.Task,
            Interval = ValidateJobInterval(job.Interval),
            Status = JobStatus.Pending,
            AutoRestart = _options.EnableAutoRestart,
            MaxRetryAttempts = _options.DefaultMaxRetryAttempts
        };

        return RegisterJobInternal(trackedJob);
    }

    /// <summary>
    /// Creates a new tracked job with full configuration options.
    /// </summary>
    /// <param name="trackedJob">The TrackedAmiquinJob to register.</param>
    /// <returns>True if the job was successfully created; otherwise, false.</returns>
    public bool CreateTrackedJob(TrackedAmiquinJob trackedJob)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot create tracked job - JobService is disposed");
            return false;
        }

        _logger.LogInformation("Creating tracked job {JobName} [{JobId}]", trackedJob.Name, trackedJob.Id);
        
        if (string.IsNullOrEmpty(trackedJob.Id) || trackedJob.Task is null)
        {
            _logger.LogError("Tracked job {JobName} does not have a valid task or ID", trackedJob.Name);
            return false;
        }

        if (_dynamicJobs.ContainsKey(trackedJob.Id))
        {
            _logger.LogWarning("Tracked job {JobName} [{JobId}] already exists", trackedJob.Name, trackedJob.Id);
            return false;
        }

        trackedJob.Interval = ValidateJobInterval(trackedJob.Interval);
        return RegisterJobInternal(trackedJob);
    }

    /// <summary>
    /// Pauses a job by its ID.
    /// </summary>
    /// <param name="jobId">The ID of the job to pause.</param>
    /// <returns>True if the job was successfully paused; otherwise, false.</returns>
    public bool PauseJob(string jobId)
    {
        if (!_dynamicJobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Cannot pause job [{JobId}] - job not found", jobId);
            return false;
        }

        if (_jobCancellationTokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
        }

        job.Status = JobStatus.Paused;
        job.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Job {JobName} [{JobId}] has been paused", job.Name, jobId);
        return true;
    }

    /// <summary>
    /// Resumes a paused job by its ID.
    /// </summary>
    /// <param name="jobId">The ID of the job to resume.</param>
    /// <returns>True if the job was successfully resumed; otherwise, false.</returns>
    public bool ResumeJob(string jobId)
    {
        if (!_dynamicJobs.TryGetValue(jobId, out var job))
        {
            _logger.LogWarning("Cannot resume job [{JobId}] - job not found", jobId);
            return false;
        }

        if (job.Status != JobStatus.Paused)
        {
            _logger.LogWarning("Cannot resume job {JobName} [{JobId}] - job is not paused (current status: {Status})", job.Name, jobId, job.Status);
            return false;
        }

        job.Status = JobStatus.Pending;
        job.UpdatedAt = DateTime.UtcNow;
        ScheduleJobExecution(job);
        _logger.LogInformation("Job {JobName} [{JobId}] has been resumed", job.Name, jobId);
        return true;
    }

    /// <summary>
    /// Cancels a job by its ID.
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel.</param>
    /// <returns>True if the job was successfully cancelled; otherwise, false.</returns>
    public bool CancelJob(string jobId)
    {
        if (!_dynamicJobs.TryRemove(jobId, out var job))
        {
            _logger.LogWarning("Cannot cancel job [{JobId}] - job not found", jobId);
            return false;
        }

        if (_jobCancellationTokens.TryRemove(jobId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }

        if (_jobTimers.TryRemove(jobId, out var timer))
        {
            timer.Dispose();
        }

        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Job {JobName} [{JobId}] has been cancelled", job.Name, jobId);
        return true;
    }

    /// <summary>
    /// Gets job information by its ID.
    /// </summary>
    /// <param name="jobId">The ID of the job to retrieve.</param>
    /// <returns>The TrackedAmiquinJob if found; otherwise, null.</returns>
    public TrackedAmiquinJob? GetJob(string jobId)
    {
        return _dynamicJobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        await _jobManagementLock.WaitAsync();
        try
        {
            _logger.LogInformation("Disposing JobService with {JobCount} jobs", _dynamicJobs.Count);

            // Cancel all running jobs
            var cancellationTasks = new List<Task>();
            foreach (var (jobId, cts) in _jobCancellationTokens)
            {
                try
                {
                    cts.Cancel();
                    cancellationTasks.Add(Task.Run(() => cts.Dispose()));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cancelling job [{JobId}]", jobId);
                }
            }

            // Wait for cancellations with timeout
            try
            {
                await Task.WhenAll(cancellationTasks).WaitAsync(TimeSpan.FromSeconds(Constants.Timeouts.JobCancellationTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Timeout waiting for job cancellations to complete");
            }

            // Dispose all timers
            foreach (var (jobId, timer) in _jobTimers)
            {
                try
                {
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing timer for job [{JobId}]", jobId);
                }
            }

            // Update job statuses
            foreach (var job in _dynamicJobs.Values)
            {
                if (job.Status == JobStatus.Running || job.Status == JobStatus.Pending)
                {
                    job.Status = JobStatus.Cancelled;
                    job.UpdatedAt = DateTime.UtcNow;
                }
            }

            // Clear collections
            _dynamicJobs.Clear();
            _jobTimers.Clear();
            _jobCancellationTokens.Clear();

            _logger.LogInformation("JobService disposed successfully");
        }
        finally
        {
            _jobManagementLock.Release();
            _jobManagementLock.Dispose();
        }
    }

    /// <summary>
    /// Validates that a job interval is within acceptable bounds.
    /// </summary>
    /// <param name="interval">The interval to validate.</param>
    /// <returns>A validated interval within bounds.</returns>
    private TimeSpan ValidateJobInterval(TimeSpan interval)
    {
        var minInterval = TimeSpan.FromSeconds(_options.MinimumJobIntervalSeconds);
        var maxInterval = TimeSpan.FromSeconds(_options.MaximumJobIntervalSeconds);

        if (interval < minInterval)
        {
            _logger.LogWarning("Job interval {Interval}s is below minimum {MinInterval}s, adjusting", interval.TotalSeconds, minInterval.TotalSeconds);
            return minInterval;
        }

        if (interval > maxInterval)
        {
            _logger.LogWarning("Job interval {Interval}s is above maximum {MaxInterval}s, adjusting", interval.TotalSeconds, maxInterval.TotalSeconds);
            return maxInterval;
        }

        return interval;
    }

    /// <summary>
    /// Internal method to register a tracked job with the TaskManager.
    /// </summary>
    /// <param name="job">The TrackedAmiquinJob to register.</param>
    /// <returns>True if registration succeeded; otherwise, false.</returns>
    private bool RegisterJobInternal(TrackedAmiquinJob job)
    {
        try
        {
            if (!_dynamicJobs.TryAdd(job.Id, job))
            {
                _logger.LogError("Failed to add job {JobName} [{JobId}] to collection", job.Name, job.Id);
                return false;
            }

            // Create cancellation token source for this job
            var cts = new CancellationTokenSource();
            _jobCancellationTokens.TryAdd(job.Id, cts);

            // Set initial status and schedule first execution
            job.Status = JobStatus.Pending;
            job.NextExecutionAt = DateTime.UtcNow.Add(job.Interval);
            job.UpdatedAt = DateTime.UtcNow;
            
            // Schedule the job execution
            ScheduleJobExecution(job);

            _logger.LogInformation("Successfully registered job {JobName} [{JobId}] with {Interval}s interval", 
                job.Name, job.Id, job.Interval.TotalSeconds);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering job {JobName} [{JobId}]", job.Name, job.Id);
            return false;
        }
    }

    /// <summary>
    /// Schedules a job for execution using a timer.
    /// </summary>
    /// <param name="job">The job to schedule.</param>
    private void ScheduleJobExecution(TrackedAmiquinJob job)
    {
        if (_disposed || !_jobCancellationTokens.TryGetValue(job.Id, out var cts) || cts.IsCancellationRequested)
        {
            return;
        }

        // Dispose existing timer if it exists
        if (_jobTimers.TryRemove(job.Id, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new timer for this job execution
        var timer = new Timer(async _ => await ExecuteJobSafely(job), null, job.Interval, Timeout.InfiniteTimeSpan);
        _jobTimers.TryAdd(job.Id, timer);
    }

    /// <summary>
    /// Safely executes a job using the TaskManager with proper error handling and retry logic.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    private async Task ExecuteJobSafely(TrackedAmiquinJob job)
    {
        if (_disposed || !_jobCancellationTokens.TryGetValue(job.Id, out var cts) || cts.IsCancellationRequested)
        {
            return;
        }

        var requestId = $"{job.Id}-{Guid.NewGuid():N}";
        
        try
        {
            job.Status = JobStatus.Running;
            job.LastExecutedAt = DateTime.UtcNow;
            job.UpdatedAt = DateTime.UtcNow;
            job.ExecutionCount++;
            job.CurrentRetryAttempt = 0;
            job.LastError = null;

            _logger.LogDebug("Executing job {JobName} [{JobId}] - execution #{ExecutionCount}", 
                job.Name, job.Id, job.ExecutionCount);

            // Execute the job using TaskManager for better resource management
            var result = await _taskManager.ExternalExecuteAsync<TrackedAmiquinJob>(
                instanceId: "job-service",
                requestId: requestId,
                task: () => ExecuteJobTask(job, cts.Token),
                cancellationToken: cts.Token);

            // Set the result to complete the TaskManager tracking
            job.RequestId = requestId;
            _taskManager.SetTaskResult(requestId, job);

            job.Status = JobStatus.Completed;
            job.UpdatedAt = DateTime.UtcNow;
            job.NextExecutionAt = DateTime.UtcNow.Add(job.Interval);
            
            _logger.LogDebug("Job {JobName} [{JobId}] completed successfully", job.Name, job.Id);

            // Schedule next execution if job is still active and not paused
            if (!cts.IsCancellationRequested && _dynamicJobs.ContainsKey(job.Id))
            {
                ScheduleJobExecution(job);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Job {JobName} [{JobId}] was cancelled", job.Name, job.Id);
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.LastError = ex.Message;
            job.UpdatedAt = DateTime.UtcNow;
            job.CurrentRetryAttempt++;

            _logger.LogError(ex, "Job {JobName} [{JobId}] failed (attempt {RetryAttempt}/{MaxRetries}): {Error}", 
                job.Name, job.Id, job.CurrentRetryAttempt, job.MaxRetryAttempts, ex.Message);

            // Handle retry logic
            if (job.AutoRestart && job.CurrentRetryAttempt < job.MaxRetryAttempts && !cts.IsCancellationRequested)
            {
                var retryDelay = CalculateRetryDelay(job.CurrentRetryAttempt);
                _logger.LogInformation("Scheduling retry for job {JobName} [{JobId}] in {RetryDelay}s", 
                    job.Name, job.Id, retryDelay.TotalSeconds);
                
                // Schedule retry with exponential backoff
                var retryTimer = new Timer(async _ => await ExecuteJobSafely(job), null, retryDelay, Timeout.InfiniteTimeSpan);
                
                // Replace the existing timer
                if (_jobTimers.TryGetValue(job.Id, out var oldTimer))
                {
                    oldTimer.Dispose();
                }
                _jobTimers.TryAdd(job.Id, retryTimer);
            }
            else
            {
                _logger.LogError("Job {JobName} [{JobId}] exceeded maximum retry attempts or auto-restart is disabled", job.Name, job.Id);
            }
        }
    }

    /// <summary>
    /// Executes the actual job task with proper scoping.
    /// </summary>
    /// <param name="job">The job to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task ExecuteJobTask(TrackedAmiquinJob job, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        await job.Task(_serviceScopeFactory, cancellationToken);
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff.
    /// </summary>
    /// <param name="retryAttempt">The current retry attempt number.</param>
    /// <returns>The delay before the next retry attempt.</returns>
    private TimeSpan CalculateRetryDelay(int retryAttempt)
    {
        // Exponential backoff: 2^attempt * 5 seconds, capped at 5 minutes
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, retryAttempt) * 5, 300));
        return delay;
    }

    /// <summary>
    /// Gets statistics about the job service.
    /// </summary>
    /// <returns>A dictionary containing job service statistics.</returns>
    public Dictionary<string, object> GetStatistics()
    {
        var stats = new Dictionary<string, object>
        {
            ["TotalJobs"] = _dynamicJobs.Count,
            ["PendingJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Pending),
            ["RunningJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Running),
            ["CompletedJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Completed),
            ["FailedJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Failed),
            ["PausedJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Paused),
            ["CancelledJobs"] = _dynamicJobs.Values.Count(j => j.Status == JobStatus.Cancelled),
            ["ActiveTimers"] = _jobTimers.Count,
            ["TotalExecutions"] = _dynamicJobs.Values.Sum(j => j.ExecutionCount)
        };

        return stats;
    }
}