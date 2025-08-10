namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for the JobService and TaskManager integration.
/// </summary>
public class JobManagerOptions
{
    /// <summary>
    /// Configuration section name for job manager options.
    /// </summary>
    public const string SectionName = "JobManager";

    /// <summary>
    /// Frequency of health check timer in seconds. Default is 300 seconds (5 minutes).
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of pending jobs allowed before queueing new requests. Default is 100.
    /// </summary>
    public int MaxPendingJobs { get; set; } = 100;

    /// <summary>
    /// Maximum number of pending streams allowed before queueing new requests. Default is 50.
    /// </summary>
    public int MaxPendingStreams { get; set; } = 50;

    /// <summary>
    /// Maximum number of timeout monitors allowed. This should match MaxPendingStreams. Default is 50.
    /// </summary>
    public int MaxTimeoutMonitors { get; set; } = 50;

    /// <summary>
    /// Default timeout for jobs in seconds. Default is 60 seconds (1 minute).
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum time to wait in the queue before timing out in seconds. Default is 60 seconds.
    /// </summary>
    public int MaxQueueTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to enable automatic job restart on failure. Default is true.
    /// </summary>
    public bool EnableAutoRestart { get; set; } = true;

    /// <summary>
    /// Default maximum retry attempts for failed jobs. Default is 3.
    /// </summary>
    public int DefaultMaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Minimum interval between job executions in seconds. Default is 5 seconds.
    /// </summary>
    public int MinimumJobIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum interval between job executions in seconds. Default is 86400 seconds (1 day).
    /// </summary>
    public int MaximumJobIntervalSeconds { get; set; } = 86400;

    /// <summary>
    /// Validates the configuration values and ensures they are within acceptable ranges.
    /// </summary>
    public void Validate()
    {
        if (HealthCheckIntervalSeconds <= 0)
            throw new ArgumentException("HealthCheckIntervalSeconds must be greater than 0", nameof(HealthCheckIntervalSeconds));

        if (MaxPendingJobs <= 0)
            throw new ArgumentException("MaxPendingJobs must be greater than 0", nameof(MaxPendingJobs));

        if (MaxPendingStreams <= 0)
            throw new ArgumentException("MaxPendingStreams must be greater than 0", nameof(MaxPendingStreams));

        if (MaxTimeoutMonitors <= 0)
            throw new ArgumentException("MaxTimeoutMonitors must be greater than 0", nameof(MaxTimeoutMonitors));

        if (DefaultTimeoutSeconds <= 0)
            throw new ArgumentException("DefaultTimeoutSeconds must be greater than 0", nameof(DefaultTimeoutSeconds));

        if (MaxQueueTimeoutSeconds <= 0)
            throw new ArgumentException("MaxQueueTimeoutSeconds must be greater than 0", nameof(MaxQueueTimeoutSeconds));

        if (DefaultMaxRetryAttempts < 0)
            throw new ArgumentException("DefaultMaxRetryAttempts cannot be negative", nameof(DefaultMaxRetryAttempts));

        if (MinimumJobIntervalSeconds <= 0)
            throw new ArgumentException("MinimumJobIntervalSeconds must be greater than 0", nameof(MinimumJobIntervalSeconds));

        if (MaximumJobIntervalSeconds <= MinimumJobIntervalSeconds)
            throw new ArgumentException("MaximumJobIntervalSeconds must be greater than MinimumJobIntervalSeconds", nameof(MaximumJobIntervalSeconds));

        // Ensure reasonable upper bounds
        if (HealthCheckIntervalSeconds > Constants.Limits.MaxHealthCheckIntervalSeconds) // 1 hour max
            throw new ArgumentException($"HealthCheckIntervalSeconds cannot exceed {Constants.Limits.MaxHealthCheckIntervalSeconds} seconds (1 hour)", nameof(HealthCheckIntervalSeconds));

        if (DefaultTimeoutSeconds > Constants.Limits.MaxJobTimeoutSeconds) // 1 hour max
            throw new ArgumentException($"DefaultTimeoutSeconds cannot exceed {Constants.Limits.MaxJobTimeoutSeconds} seconds (1 hour)", nameof(DefaultTimeoutSeconds));

        if (MaxQueueTimeoutSeconds > Constants.Limits.MaxQueueTimeoutSeconds) // 30 minutes max
            throw new ArgumentException($"MaxQueueTimeoutSeconds cannot exceed {Constants.Limits.MaxQueueTimeoutSeconds} seconds (30 minutes)", nameof(MaxQueueTimeoutSeconds));

        if (MaxPendingJobs > Constants.Limits.MaxPendingJobsLimit) // Reasonable limit
            throw new ArgumentException($"MaxPendingJobs cannot exceed {Constants.Limits.MaxPendingJobsLimit}", nameof(MaxPendingJobs));
    }
}