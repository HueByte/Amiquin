using Amiquin.Core.Job.Models;

namespace Amiquin.Core.Job;

public interface IJobService
{
    bool CreateDynamicJob(AmiquinJob job);
    ValueTask DisposeAsync();
    void StartRunnableJobs();
}
