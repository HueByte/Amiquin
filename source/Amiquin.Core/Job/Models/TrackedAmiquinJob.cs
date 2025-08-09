using Jiro.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Core.Job.Models;

/// <summary>
/// Represents a tracked Amiquin job that can be managed by TaskManager.
/// Extends TrackedObject to support request tracking and external task completion.
/// </summary>
public class TrackedAmiquinJob : TrackedObject
{
    /// <summary>
    /// Gets or sets the unique identifier for this job.
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    /// Gets or sets the display name of the job.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Gets or sets the description of what this job does.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Gets or sets when this job was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this job was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID this job is associated with (0 for global jobs).
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the task function to execute.
    /// </summary>
    public Func<IServiceScopeFactory, CancellationToken, Task> Task { get; set; } = default!;

    /// <summary>
    /// Gets or sets the interval between job executions.
    /// </summary>
    public TimeSpan Interval { get; set; }

    /// <summary>
    /// Gets or sets the job execution status.
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    /// Gets or sets the last execution timestamp.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the next scheduled execution timestamp.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Gets or sets the number of times this job has been executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the last error message if execution failed.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets whether this job should automatically restart on failure.
    /// </summary>
    public bool AutoRestart { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts on failure.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the current retry attempt count.
    /// </summary>
    public int CurrentRetryAttempt { get; set; }
}

/// <summary>
/// Represents the current status of a job.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job is waiting to be executed.
    /// </summary>
    Pending,

    /// <summary>
    /// Job is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Job is paused/suspended.
    /// </summary>
    Paused
}