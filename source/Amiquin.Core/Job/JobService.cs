using System.Collections.Concurrent;
using System.Threading.Tasks;
using Amiquin.Core.Abstraction;
using Amiquin.Core.Job.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Job;

public class JobService : IAsyncDisposable, IJobService
{
    private readonly ILogger<JobService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ConcurrentDictionary<string, AmiquinJob> _dynamicJobs = new();
    private readonly ConcurrentBag<Task> _intervalRunningJobs = new();
    private CancellationTokenSource _jobCancellationToken = new();

    public JobService(ILogger<JobService> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public void StartRunnableJobs()
    {
        _logger.LogInformation("Registering runnable jobs");

        using var scope = _serviceScopeFactory.CreateScope();
        var runnableJobs = scope.ServiceProvider.GetServices<IRunnableJob>();

        foreach (var runnableJob in runnableJobs)
        {
            RegisterJobInternal(new AmiquinJob
            {
                Id = Guid.NewGuid().ToString(),
                Name = runnableJob.GetType().Name,
                Description = string.Empty, // Add attribute later
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                GuildId = 0,
                Task = runnableJob.RunAsync,
                Interval = TimeSpan.FromHours(8) // Add attribute later
            });
        }
    }

    public bool CreateDynamicJob(AmiquinJob job)
    {
        _logger.LogInformation("Creating job {JobName}", job.Name);
        if (string.IsNullOrEmpty(job.Id) || job.Task is null)
        {
            _logger.LogError("Job {JobName} does not have a task or ID", job.Name);
            return false;
        }

        if (_dynamicJobs.ContainsKey(job.Id))
        {
            _logger.LogWarning("Job {JobName} already exists", job.Name);
            return false;
        }

        var cacheResult = _dynamicJobs.TryAdd(job.Id, job);
        RegisterJobInternal(job);

        return cacheResult;
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing all jobs");
        await _jobCancellationToken.CancelAsync();

        foreach (var job in _dynamicJobs)
        {
            _logger.LogInformation("Removing job {JobName}", job.Value.Name);
            _dynamicJobs.TryRemove(job.Key, out var cachedJob);
        }

        foreach (var runningJob in _intervalRunningJobs)
        {
            _logger.LogInformation("Disposing running job");
            await Task.Run(async () => await runningJob, new CancellationTokenSource(4000).Token);
            runningJob.Dispose();
        }
    }

    private void RegisterJobInternal(AmiquinJob job)
    {
        AmiquinJob scopedJob = job;
        IServiceScopeFactory scopedServiceScopeFactoryRef = _serviceScopeFactory;
        ILogger<JobService> scopedLoggerRef = _logger;
        CancellationTokenSource scopedJobCancellationTokenRef = _jobCancellationToken;

        _logger.LogInformation("Registering job {JobName}", scopedJob.Name);

        _intervalRunningJobs.Add(Task.Run(async () =>
        {
            while (!scopedJobCancellationTokenRef.IsCancellationRequested || scopedJob.Interval != TimeSpan.Zero)
            {
                scopedLoggerRef.LogInformation("Running job {JobName}", scopedJob.Name);
                await job.Task(scopedServiceScopeFactoryRef, scopedJobCancellationTokenRef.Token);
                scopedLoggerRef.LogInformation("Job {JobName} completed", scopedJob.Name);

                await Task.Delay(job.Interval, scopedJobCancellationTokenRef.Token);
            }
        }));

        _logger.LogInformation("Job {JobName} registered", scopedJob.Name);
    }
}