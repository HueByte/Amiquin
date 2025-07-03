using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Amiquin.Core.Cleaner;

/// <summary>
/// Service implementation for cleaning and maintenance operations.
/// Performs scheduled cleanup of caches and temporary data to maintain system performance.
/// </summary>
public class CleanerService : ICleanerService
{
    /// <summary>
    /// Gets or sets the frequency in seconds for running the cleanup job.
    /// Default is 3600 seconds (1 hour).
    /// </summary>
    public int FrequencyInSeconds { get; set; } = 3600; // Default to 1 hour

    /// <summary>
    /// Runs the cleanup operations asynchronously.
    /// </summary>
    /// <param name="serviceScopeFactory">Factory for creating service scopes.</param>
    /// <param name="cancellationToken">Cancellation token for stopping the operation.</param>
    public Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CleanerService>>();
        var messageCacheService = scope.ServiceProvider.GetRequiredService<IMessageCacheService>();
        var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

        logger.LogInformation("Clearing message cache");
        messageCacheService.ClearMessageCachce();
        logger.LogInformation("Message cache cleared");

        if (cache is MemoryCache memoryCache)
        {
            logger.LogInformation("Clearing memory cache | Cache count: {cacheCount}", memoryCache.Count);

            StringBuilder sb = new();
            sb.AppendLine("Cache keys:");
            foreach (var item in memoryCache.Keys)
            {
                sb.AppendLine(item.ToString());
            }

            memoryCache.Clear();
            logger.LogInformation("Memory cache cleared");
        }

        return Task.CompletedTask;
    }
}