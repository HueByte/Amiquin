using System.Text;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.PeriodicalLogger;

public class PeriodicalLoggerService : IPeriodicalLoggerService
{
    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PeriodicalLoggerService>>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();

        StringBuilder sb = new();
        sb.AppendLine("Periodical logger");
        sb.AppendLine("--- Discord info ---");
        sb.AppendLine($"Guilds: {discordClient.Guilds.Count}");
        sb.AppendLine($"Channels: {discordClient.Guilds.SelectMany(x => x.Channels).Count()}");
        sb.AppendLine($"Users: {discordClient.Guilds.Select(x => x.MemberCount).Count()}");
        sb.AppendLine($"Latency: {discordClient.Latency}ms");
        sb.AppendLine($"Connection State: {discordClient.ConnectionState}");
        sb.AppendLine($"Status: {discordClient.Status}");
        sb.AppendLine($"Activity: {discordClient.Activity}");

        if (memoryCache is MemoryCache cache)
        {
            sb.AppendLine("--- Cache info ---");
            sb.AppendLine($"Memory Cache Count: {cache.Count}");
        }

        logger.LogInformation(sb.ToString());
        await Task.CompletedTask;
    }
}