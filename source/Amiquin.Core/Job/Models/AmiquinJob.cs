using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Core.Job.Models;

public class AmiquinJob
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong GuildId { get; set; }
    public Func<IServiceScopeFactory, CancellationToken, Task> Task { get; set; } = default!;
    public TimeSpan Interval { get; set; }
}