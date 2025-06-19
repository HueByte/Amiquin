using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Core.Abstraction;

public interface IRunnableJob
{
    int FrequencyInSeconds { get; set; }
    Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken);
}