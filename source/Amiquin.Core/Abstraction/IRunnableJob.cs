using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Core.Abstraction;

public interface IRunnableJob
{
    Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken);
}