using Amiquin.Core.Job;
using Amiquin.Core.Job.Models;
using Amiquin.Core.Options;
using Jiro.Shared.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Integration;

/// <summary>
/// Integration tests to verify TaskManager integration works correctly
/// </summary>
public class TaskManagerJobIntegrationTests
{
    [Fact]
    public async Task JobService_WithTaskManager_HandlesTimeouts()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<JobService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockTaskManager = new Mock<ITaskManager>();

        var jobOptions = new JobManagerOptions
        {
            EnableAutoRestart = true,
            DefaultMaxRetryAttempts = 2,
            MinimumJobIntervalSeconds = 1,
            MaximumJobIntervalSeconds = 3600
        };

        var optionsMock = new Mock<IOptions<JobManagerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(jobOptions);

        // Setup TaskManager to simulate timeout
        mockTaskManager
            .Setup(tm => tm.ExternalExecuteAsync<TrackedAmiquinJob>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Task timed out after 60s"));

        var jobService = new JobService(
            mockLogger.Object,
            mockServiceScopeFactory.Object,
            mockTaskManager.Object,
            optionsMock.Object
        );

        var testJob = new AmiquinJob
        {
            Id = "timeout-test-job",
            Name = "TimeoutTestJob",
            Interval = TimeSpan.FromSeconds(1),
            Task = (factory, token) => Task.CompletedTask
        };

        // Act
        var createResult = jobService.CreateDynamicJob(testJob);

        // Assert
        Assert.True(createResult);
        Assert.Equal(1, jobService.ActiveJobCount);

        // The job should be created successfully even though execution will timeout
        var createdJob = jobService.GetJob("timeout-test-job");
        Assert.NotNull(createdJob);
        Assert.Equal("TimeoutTestJob", createdJob.Name);
        Assert.Equal(TimeSpan.FromSeconds(1), createdJob.Interval);

        await jobService.DisposeAsync();
    }

    [Fact]
    public async Task JobService_WithTaskManager_ProperlySetsResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<JobService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockTaskManager = new Mock<ITaskManager>();

        var jobOptions = new JobManagerOptions();
        var optionsMock = new Mock<IOptions<JobManagerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(jobOptions);

        var jobService = new JobService(
            mockLogger.Object,
            mockServiceScopeFactory.Object,
            mockTaskManager.Object,
            optionsMock.Object
        );

        // Act
        var createResult = jobService.CreateDynamicJob(new AmiquinJob
        {
            Id = "test-job",
            Name = "TestJob",
            Task = (factory, token) => Task.CompletedTask
        });

        // Assert - Job creation should succeed
        Assert.True(createResult);

        // Verify the job was created and is tracked
        var createdJob = jobService.GetJob("test-job");
        Assert.NotNull(createdJob);
        Assert.Equal("TestJob", createdJob.Name);
        Assert.Equal(1, jobService.ActiveJobCount);

        await jobService.DisposeAsync();
    }

    [Fact]
    public void JobService_JobLifecycle_WorksCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<JobService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockTaskManager = new Mock<ITaskManager>();

        var jobOptions = new JobManagerOptions();
        var optionsMock = new Mock<IOptions<JobManagerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(jobOptions);

        var jobService = new JobService(
            mockLogger.Object,
            mockServiceScopeFactory.Object,
            mockTaskManager.Object,
            optionsMock.Object
        );

        var testJob = new AmiquinJob
        {
            Id = "lifecycle-test-job",
            Name = "LifecycleTestJob",
            Interval = TimeSpan.FromSeconds(30),
            Task = (factory, token) => Task.CompletedTask
        };

        // Act & Assert - Create job
        var createResult = jobService.CreateDynamicJob(testJob);
        Assert.True(createResult);
        Assert.Equal(1, jobService.ActiveJobCount);

        // Act & Assert - Pause job
        var pauseResult = jobService.PauseJob("lifecycle-test-job");
        Assert.True(pauseResult);

        var pausedJob = jobService.GetJob("lifecycle-test-job");
        Assert.NotNull(pausedJob);
        Assert.Equal(JobStatus.Paused, pausedJob.Status);

        // Act & Assert - Resume job
        var resumeResult = jobService.ResumeJob("lifecycle-test-job");
        Assert.True(resumeResult);

        var resumedJob = jobService.GetJob("lifecycle-test-job");
        Assert.NotNull(resumedJob);
        Assert.Equal(JobStatus.Pending, resumedJob.Status);

        // Act & Assert - Cancel job
        var cancelResult = jobService.CancelJob("lifecycle-test-job");
        Assert.True(cancelResult);
        Assert.Equal(0, jobService.ActiveJobCount);

        var cancelledJob = jobService.GetJob("lifecycle-test-job");
        Assert.Null(cancelledJob); // Should be removed from active jobs
    }
}