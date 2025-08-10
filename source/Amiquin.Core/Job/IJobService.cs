using Amiquin.Core.Job.Models;

namespace Amiquin.Core.Job;

public interface IJobService
{
    bool CreateDynamicJob(AmiquinJob job);
    bool CreateTrackedJob(TrackedAmiquinJob trackedJob);
    bool CancelJob(string jobId);
    ValueTask DisposeAsync();
    void StartRunnableJobs();
}