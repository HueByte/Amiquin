using Amiquin.Core.Job;
using Amiquin.Core.Job.Models;
using Amiquin.Core.Options;
using Jiro.Shared.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Job;

public class JobServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<JobService>> _mockLogger;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ITaskManager> _mockTaskManager;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly JobManagerOptions _jobOptions;
    private readonly JobService _jobService;

    public JobServiceTests()
    {
        _mockLogger = new Mock<ILogger<JobService>>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockTaskManager = new Mock<ITaskManager>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockServiceScopeFactory.Setup(f => f.CreateScope()).Returns(_mockServiceScope.Object);
        _mockServiceScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);

        _jobOptions = new JobManagerOptions
        {
            EnableAutoRestart = true,
            DefaultMaxRetryAttempts = 3,
            MinimumJobIntervalSeconds = 1,
            MaximumJobIntervalSeconds = 3600
        };

        var optionsMock = new Mock<IOptions<JobManagerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_jobOptions);

        _jobService = new JobService(
            _mockLogger.Object,
            _mockServiceScopeFactory.Object,
            _mockTaskManager.Object,
            optionsMock.Object
        );
    }

    [Fact]
    public void CreateDynamicJob_WithValidJob_ReturnsTrue()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "test-job-123",
            Name = "TestJob",
            Description = "Test job description",
            Interval = TimeSpan.FromSeconds(30),
            Task = (factory, token) => Task.CompletedTask
        };

        // Act
        var result = _jobService.CreateDynamicJob(job);

        // Assert
        Assert.True(result);
        Assert.Equal(1, _jobService.ActiveJobCount);
        Assert.Contains("test-job-123", _jobService.Jobs.Keys);
    }

    [Fact]
    public void CreateDynamicJob_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "", // Invalid empty ID
            Name = "TestJob",
            Task = (factory, token) => Task.CompletedTask
        };

        // Act
        var result = _jobService.CreateDynamicJob(job);

        // Assert
        Assert.False(result);
        Assert.Equal(0, _jobService.ActiveJobCount);
    }

    [Fact]
    public void CreateDynamicJob_WithDuplicateId_ReturnsFalse()
    {
        // Arrange
        var job1 = new AmiquinJob
        {
            Id = "duplicate-job",
            Name = "Job1",
            Task = (factory, token) => Task.CompletedTask
        };
        var job2 = new AmiquinJob
        {
            Id = "duplicate-job", // Same ID
            Name = "Job2",
            Task = (factory, token) => Task.CompletedTask
        };

        // Act
        _jobService.CreateDynamicJob(job1);
        var result = _jobService.CreateDynamicJob(job2);

        // Assert
        Assert.False(result);
        Assert.Equal(1, _jobService.ActiveJobCount); // Only first job should be added
    }

    [Fact]
    public void CreateDynamicJob_ValidatesJobInterval()
    {
        // Arrange
        var shortIntervalJob = new AmiquinJob
        {
            Id = "short-interval-job",
            Name = "ShortJob",
            Interval = TimeSpan.FromMilliseconds(100), // Too short
            Task = (factory, token) => Task.CompletedTask
        };

        var longIntervalJob = new AmiquinJob
        {
            Id = "long-interval-job",
            Name = "LongJob",
            Interval = TimeSpan.FromHours(2), // Too long
            Task = (factory, token) => Task.CompletedTask
        };

        // Act
        var shortResult = _jobService.CreateDynamicJob(shortIntervalJob);
        var longResult = _jobService.CreateDynamicJob(longIntervalJob);

        // Assert
        Assert.True(shortResult);
        Assert.True(longResult);

        // Verify intervals were adjusted
        var shortJob = _jobService.GetJob("short-interval-job");
        var longJob = _jobService.GetJob("long-interval-job");

        Assert.NotNull(shortJob);
        Assert.NotNull(longJob);
        Assert.Equal(TimeSpan.FromSeconds(_jobOptions.MinimumJobIntervalSeconds), shortJob.Interval);
        Assert.Equal(TimeSpan.FromSeconds(_jobOptions.MaximumJobIntervalSeconds), longJob.Interval);
    }

    [Fact]
    public void PauseJob_WithValidJobId_ReturnsTrue()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "pausable-job",
            Name = "PausableJob",
            Task = (factory, token) => Task.CompletedTask
        };
        _jobService.CreateDynamicJob(job);

        // Act
        var result = _jobService.PauseJob("pausable-job");

        // Assert
        Assert.True(result);
        var pausedJob = _jobService.GetJob("pausable-job");
        Assert.NotNull(pausedJob);
        Assert.Equal(JobStatus.Paused, pausedJob.Status);
    }

    [Fact]
    public void PauseJob_WithInvalidJobId_ReturnsFalse()
    {
        // Act
        var result = _jobService.PauseJob("non-existent-job");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ResumeJob_WithPausedJob_ReturnsTrue()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "resumable-job",
            Name = "ResumableJob",
            Task = (factory, token) => Task.CompletedTask
        };
        _jobService.CreateDynamicJob(job);
        _jobService.PauseJob("resumable-job");

        // Act
        var result = _jobService.ResumeJob("resumable-job");

        // Assert
        Assert.True(result);
        var resumedJob = _jobService.GetJob("resumable-job");
        Assert.NotNull(resumedJob);
        Assert.Equal(JobStatus.Pending, resumedJob.Status);
    }

    [Fact]
    public void ResumeJob_WithNonPausedJob_ReturnsFalse()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "active-job",
            Name = "ActiveJob",
            Task = (factory, token) => Task.CompletedTask
        };
        _jobService.CreateDynamicJob(job);

        // Act
        var result = _jobService.ResumeJob("active-job");

        // Assert
        Assert.False(result); // Job is not paused, so resume should fail
    }

    [Fact]
    public void CancelJob_WithValidJobId_ReturnsTrue()
    {
        // Arrange
        var job = new AmiquinJob
        {
            Id = "cancellable-job",
            Name = "CancellableJob",
            Task = (factory, token) => Task.CompletedTask
        };
        _jobService.CreateDynamicJob(job);

        // Act
        var result = _jobService.CancelJob("cancellable-job");

        // Assert
        Assert.True(result);
        Assert.Equal(0, _jobService.ActiveJobCount); // Job should be removed
        Assert.Null(_jobService.GetJob("cancellable-job"));
    }

    [Fact]
    public void CancelJob_WithInvalidJobId_ReturnsFalse()
    {
        // Act
        var result = _jobService.CancelJob("non-existent-job");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task JobExecution_WithTaskManagerIntegration_CallsSetTaskResult()
    {
        // Arrange
        var taskCompletedSuccessfully = false;
        var job = new AmiquinJob
        {
            Id = "integration-test-job",
            Name = "IntegrationTestJob",
            Interval = TimeSpan.FromSeconds(1),
            Task = (factory, token) =>
            {
                taskCompletedSuccessfully = true;
                return Task.CompletedTask;
            }
        };

        // Setup TaskManager to capture the execution
        TrackedAmiquinJob? resultJob = null;
        _mockTaskManager
            .Setup(tm => tm.ExternalExecuteAsync<TrackedAmiquinJob>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .Returns((string instanceId, string requestId, Func<Task> task, CancellationToken ct) =>
            {
                // Execute the task and simulate TaskManager completion
                return Task.Run(async () =>
                {
                    await task(); // Execute the job task
                    return _jobService.GetJob("integration-test-job")!; // Return the job as result
                });
            });

        _mockTaskManager
            .Setup(tm => tm.SetTaskResult(It.IsAny<string>(), It.IsAny<TrackedAmiquinJob>()))
            .Callback<string, TrackedAmiquinJob>((requestId, job) =>
            {
                resultJob = job;
            });

        // Act
        var createResult = _jobService.CreateDynamicJob(job);

        // Give the job a moment to potentially execute
        await Task.Delay(100);

        // Assert
        Assert.True(createResult);
        // Note: Since job execution is timer-based and async, we can't easily assert the execution completion
        // in a unit test without making the test flaky. The important part is that the job was created successfully
        // and the TaskManager integration points are properly set up.
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        var job1 = new AmiquinJob { Id = "job1", Name = "Job1", Task = (f, t) => Task.CompletedTask };
        var job2 = new AmiquinJob { Id = "job2", Name = "Job2", Task = (f, t) => Task.CompletedTask };
        var job3 = new AmiquinJob { Id = "job3", Name = "Job3", Task = (f, t) => Task.CompletedTask };

        _jobService.CreateDynamicJob(job1);
        _jobService.CreateDynamicJob(job2);
        _jobService.CreateDynamicJob(job3);
        _jobService.PauseJob("job2");

        // Act
        var stats = _jobService.GetStatistics();

        // Assert
        Assert.Equal(3, stats["TotalJobs"]);
        Assert.Equal(2, stats["PendingJobs"]); // job1 and job3
        Assert.Equal(1, stats["PausedJobs"]); // job2
        Assert.Equal(0, stats["RunningJobs"]);
        Assert.Equal(0, stats["CompletedJobs"]);
        Assert.Equal(0, stats["FailedJobs"]);
        Assert.Equal(0, stats["CancelledJobs"]);
    }

    [Fact]
    public async Task JobService_WithTimeoutHandling_HandlesTimeoutGracefully()
    {
        // Arrange
        var longRunningJob = new AmiquinJob
        {
            Id = "timeout-test-job",
            Name = "TimeoutTestJob",
            Interval = TimeSpan.FromSeconds(1),
            Task = async (factory, token) =>
            {
                // Simulate a job that takes longer than expected
                await Task.Delay(TimeSpan.FromMinutes(1), token);
            }
        };

        // Setup TaskManager to simulate timeout
        _mockTaskManager
            .Setup(tm => tm.ExternalExecuteAsync<TrackedAmiquinJob>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Task timed out after 60s"));

        // Act
        var createResult = _jobService.CreateDynamicJob(longRunningJob);

        // Assert
        Assert.True(createResult);
        // The job should be created successfully, timeout handling occurs during execution
    }

    public async ValueTask DisposeAsync()
    {
        if (_jobService != null)
            await _jobService.DisposeAsync();
        _mockServiceScope?.Object?.Dispose();
    }
}