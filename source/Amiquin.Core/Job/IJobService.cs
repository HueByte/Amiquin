using Amiquin.Core.Job.Models;

namespace Amiquin.Core.Job;

public interface IJobService
{
    bool CreateDynamicJob(AmiquinJob job);
    bool CancelJob(string jobId);
    ValueTask DisposeAsync();
    void StartRunnableJobs();
}