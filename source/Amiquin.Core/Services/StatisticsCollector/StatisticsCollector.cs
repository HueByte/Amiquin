using System.Text;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.BotSession;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.StatisticsCollector;

public class StatisticsCollector : IStatisticsCollector
{
    public int FrequencyInSeconds { get; set; } = 300;
    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<StatisticsCollector>>();
        var memoryCache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
        var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
        var statisticsRepository = scope.ServiceProvider.GetRequiredService<IBotStatisticsRepository>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var botSessionService = scope.ServiceProvider.GetRequiredService<BotSessionService>();

        var stats = await CollectBotStatisticsAsync();

        await statisticsRepository.AddAsync(stats);
        await statisticsRepository.SaveChangesAsync();


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

        async Task<BotStatistics> CollectBotStatisticsAsync()
        {
            try
            {
                int commandCount = 0;
                int failedCommandCount = 0;
                double averageCommandExecutionTime = 0;

                if (await commandLogRepository.AsQueryable().AnyAsync())
                {
                    commandCount = await commandLogRepository.AsQueryable().CountAsync(cancellationToken);
                    if (commandCount != 0)
                    {
                        failedCommandCount = await commandLogRepository.AsQueryable().CountAsync(x => x.IsSuccess == false, cancellationToken);
                        var successfulCommands = commandLogRepository.AsQueryable().Where(x => x.IsSuccess);
                        if (await successfulCommands.AnyAsync(cancellationToken))
                        {
                            averageCommandExecutionTime = await successfulCommands.AverageAsync(x => x.Duration, cancellationToken);
                        }
                        else
                        {
                            averageCommandExecutionTime = 0;
                        }
                    }
                }


                BotStatistics botStatistics = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    TotalServersCount = discordClient.Guilds.Count,
                    TotalChannelsCount = discordClient.Guilds.SelectMany(x => x.Channels).Count(),
                    TotalUsersCount = discordClient.Guilds.Select(x => x.MemberCount).Sum(),
                    Latency = discordClient.Latency,
                    CreatedDate = DateTime.UtcNow,
                    ShardCount = discordClient.Shards.Count,
                    TotalCommandsCount = commandCount,
                    TotalErrorsCount = failedCommandCount,
                    Version = config.GetValue<string>(Constants.Environment.BotVersion) ?? "Unknown",
                    AverageCommandExecutionTimeInMs = averageCommandExecutionTime,
                    CacheItems = memoryCache is MemoryCache memCache ? memCache.Count : 0,
                    CpuUsage = await botSessionService.GetCurrentCpuUsageAsync(),
                    AvailableMemoryMB = await botSessionService.GetUsedMemoryMBAsync(),
                    UsedMemoryMB = await botSessionService.GetUsedMemoryPercentageAsync(),
                    UsedMemoryPercentage = await botSessionService.GetUsedMemoryPercentageAsync(),
                    BotName = botSessionService.BotName,
                    UpTimeInSeconds = (int)(DateTime.UtcNow - botSessionService.StartedAt).TotalSeconds,
                    // UpTimeInSeconds = (int)(DateTime.UtcNow - discordClient.StartedAt).TotalSeconds,
                };

                return botStatistics;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error collecting bot statistics");
                return new BotStatistics
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedDate = DateTime.UtcNow,
                    BotName = "Amiquin",
                    Version = config.GetValue<string>(Constants.Environment.BotVersion) ?? "Unknown"
                };
            }
        }
    }
}