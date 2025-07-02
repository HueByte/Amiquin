using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Amiquin.Core.Cleaner;

public class CleanerService : ICleanerService
{
    public int FrequencyInSeconds { get; set; } = 3600; // Default to 1 hour
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