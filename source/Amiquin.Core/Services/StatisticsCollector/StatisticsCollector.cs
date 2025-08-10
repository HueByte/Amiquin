using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.BotSession;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Amiquin.Core.Services.StatisticsCollector;

/// <summary>
/// Enhanced statistics collector service with improved performance monitoring, error handling, and modular architecture.
/// Collects and persists comprehensive bot statistics including Discord metrics, performance data, and system resources.
/// </summary>
public class StatisticsCollector : IStatisticsCollector
{
    private const int DefaultFrequencySeconds = Constants.JobFrequencies.StatisticsCollectionFrequency;
    private const int DatabaseTimeoutSeconds = Constants.Timeouts.DatabaseOperationTimeoutSeconds;
    private const string LoggerPrefix = "StatisticsCollector";
    private const string UnknownVersion = Constants.DefaultValues.UnknownValue;
    private const string FallbackBotName = Constants.DefaultValues.BotName;

    public int FrequencyInSeconds { get; set; } = DefaultFrequencySeconds;

    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);

        var stopwatch = Stopwatch.StartNew();
        ILogger<StatisticsCollector>? logger = null;

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            logger = scope.ServiceProvider.GetRequiredService<ILogger<StatisticsCollector>>();

            logger.LogDebug("Starting statistics collection cycle");

            var services = ResolveServices(scope.ServiceProvider);
            var stats = await CollectBotStatisticsAsync(services, cancellationToken);

            await PersistStatisticsAsync(services.StatisticsRepository, stats, cancellationToken);
            await LogPeriodicInformationAsync(services, logger, cancellationToken);

            logger.LogDebug("Statistics collection completed successfully in {duration}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("Statistics collection was cancelled");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during statistics collection cycle");

            // Try to log basic fallback statistics
            await LogFallbackStatisticsAsync(serviceScopeFactory, ex, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Resolves all required services from the service provider.
    /// </summary>
    private static ServiceContainer ResolveServices(IServiceProvider serviceProvider)
    {
        return new ServiceContainer
        {
            Logger = serviceProvider.GetRequiredService<ILogger<StatisticsCollector>>(),
            MemoryCache = serviceProvider.GetRequiredService<IMemoryCache>(),
            DiscordClient = serviceProvider.GetRequiredService<DiscordShardedClient>(),
            CommandLogRepository = serviceProvider.GetRequiredService<ICommandLogRepository>(),
            StatisticsRepository = serviceProvider.GetRequiredService<IBotStatisticsRepository>(),
            Configuration = serviceProvider.GetRequiredService<IConfiguration>(),
            BotSessionService = serviceProvider.GetRequiredService<BotSessionService>()
        };
    }

    /// <summary>
    /// Collects comprehensive bot statistics from various sources.
    /// </summary>
    private async Task<BotStatistics> CollectBotStatisticsAsync(ServiceContainer services, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var discordMetrics = await CollectDiscordMetricsAsync(services.DiscordClient, cancellationToken);
            var commandMetrics = await CollectCommandMetricsAsync(services.CommandLogRepository, cancellationToken);
            var systemMetrics = await CollectSystemMetricsAsync(services.BotSessionService, cancellationToken);
            var cacheMetrics = CollectCacheMetrics(services.MemoryCache);

            var botName = services.BotSessionService.BotName;
            var version = services.Configuration.GetValue<string>(Constants.Environment.BotVersion) ?? UnknownVersion;

            var statistics = new BotStatistics
            {
                Id = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                BotName = !string.IsNullOrEmpty(botName) ? botName : FallbackBotName,
                Version = version,

                // Discord metrics
                TotalServersCount = discordMetrics.GuildCount,
                TotalChannelsCount = discordMetrics.ChannelCount,
                TotalUsersCount = discordMetrics.UserCount,
                ShardCount = discordMetrics.ShardCount,
                Latency = discordMetrics.Latency,

                // Command metrics
                TotalCommandsCount = commandMetrics.TotalCommands,
                TotalErrorsCount = commandMetrics.FailedCommands,
                AverageCommandExecutionTimeInMs = commandMetrics.AverageExecutionTime,

                // System metrics
                CpuUsage = systemMetrics.CpuUsage,
                AvailableMemoryMB = systemMetrics.AvailableMemoryMB,
                UsedMemoryMB = systemMetrics.UsedMemoryMB,
                UsedMemoryPercentage = systemMetrics.UsedMemoryPercentage,
                UpTimeInSeconds = systemMetrics.UptimeSeconds,

                // Cache metrics
                CacheItems = cacheMetrics.ItemCount
            };

            services.Logger.LogDebug("Statistics collection completed in {duration}ms", stopwatch.ElapsedMilliseconds);
            return statistics;
        }
        catch (Exception ex)
        {
            services.Logger.LogError(ex, "Error collecting bot statistics, returning fallback data");
            return CreateFallbackStatistics(services);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Collects Discord-specific metrics.
    /// </summary>
    private static async Task<DiscordMetrics> CollectDiscordMetricsAsync(DiscordShardedClient client, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new DiscordMetrics
            {
                GuildCount = client.Guilds.Count,
                ChannelCount = client.Guilds.SelectMany(g => g.Channels).Count(),
                UserCount = client.Guilds.Sum(g => g.MemberCount),
                ShardCount = client.Shards.Count,
                Latency = client.Latency
            };
        }, cancellationToken);
    }

    /// <summary>
    /// Collects command execution metrics from the database.
    /// </summary>
    private async Task<CommandMetrics> CollectCommandMetricsAsync(ICommandLogRepository repository, CancellationToken cancellationToken)
    {
        var queryTimeout = TimeSpan.FromSeconds(DatabaseTimeoutSeconds);
        using var timeoutCts = new CancellationTokenSource(queryTimeout);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            if (!await repository.AsQueryable().AnyAsync(combinedCts.Token))
            {
                return new CommandMetrics();
            }

            var totalCommands = await repository.AsQueryable().CountAsync(combinedCts.Token);
            if (totalCommands == 0)
            {
                return new CommandMetrics();
            }

            var failedCommands = await repository.AsQueryable()
                .CountAsync(x => !x.IsSuccess, combinedCts.Token);

            var successfulCommands = repository.AsQueryable().Where(x => x.IsSuccess);
            var averageExecutionTime = await successfulCommands.AnyAsync(combinedCts.Token)
                ? await successfulCommands.AverageAsync(x => x.Duration, combinedCts.Token)
                : 0.0;

            return new CommandMetrics
            {
                TotalCommands = totalCommands,
                FailedCommands = failedCommands,
                AverageExecutionTime = averageExecutionTime
            };
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Command metrics collection timed out after {DatabaseTimeoutSeconds} seconds");
        }
    }

    /// <summary>
    /// Collects system performance metrics.
    /// </summary>
    private async Task<SystemMetrics> CollectSystemMetricsAsync(BotSessionService sessionService, CancellationToken cancellationToken)
    {
        try
        {
            var tasks = new[]
            {
                sessionService.GetCurrentCpuUsageAsync(),
                sessionService.GetAvailableMemoryMBAsync(),
                sessionService.GetUsedMemoryMBAsync(),
                sessionService.GetUsedMemoryPercentageAsync()
            };

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Constants.Timeouts.SystemMetricsTimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var results = await Task.WhenAll(tasks);

            return new SystemMetrics
            {
                CpuUsage = results[0],
                AvailableMemoryMB = results[1],
                UsedMemoryMB = results[2],
                UsedMemoryPercentage = results[3],
                UptimeSeconds = (int)(DateTime.UtcNow - sessionService.StartedAt).TotalSeconds
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Return default values if system metrics collection fails
            return new SystemMetrics
            {
                UptimeSeconds = (int)(DateTime.UtcNow - sessionService.StartedAt).TotalSeconds
            };
        }
    }

    /// <summary>
    /// Collects memory cache metrics.
    /// </summary>
    private static CacheMetrics CollectCacheMetrics(IMemoryCache memoryCache)
    {
        return new CacheMetrics
        {
            ItemCount = memoryCache is MemoryCache cache ? cache.Count : 0
        };
    }

    /// <summary>
    /// Persists statistics to the database.
    /// </summary>
    private async Task PersistStatisticsAsync(IBotStatisticsRepository repository, BotStatistics statistics, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DatabaseTimeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await repository.AddAsync(statistics);
            await repository.SaveChangesAsync();
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            throw new TimeoutException($"Statistics persistence timed out after {DatabaseTimeoutSeconds} seconds");
        }
    }

    /// <summary>
    /// Logs periodic information about bot status.
    /// </summary>
    private async Task LogPeriodicInformationAsync(ServiceContainer services, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var discordMetrics = await CollectDiscordMetricsAsync(services.DiscordClient, cancellationToken);
            var cacheMetrics = CollectCacheMetrics(services.MemoryCache);

            var logData = new
            {
                Type = "PeriodicStatistics",
                Discord = new
                {
                    Guilds = discordMetrics.GuildCount,
                    Channels = discordMetrics.ChannelCount,
                    Users = discordMetrics.UserCount,
                    Latency = $"{discordMetrics.Latency}ms",
                    ConnectionState = services.DiscordClient.ConnectionState.ToString(),
                    Status = services.DiscordClient.Status.ToString(),
                    Activity = services.DiscordClient.Activity?.Name ?? "None"
                },
                Cache = new
                {
                    ItemCount = cacheMetrics.ItemCount
                }
            };

            logger.LogInformation("Periodic Statistics: {@LogData}", logData);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log periodic information");
        }
    }

    /// <summary>
    /// Creates fallback statistics when main collection fails.
    /// </summary>
    private BotStatistics CreateFallbackStatistics(ServiceContainer services)
    {
        return new BotStatistics
        {
            Id = Guid.NewGuid().ToString(),
            CreatedDate = DateTime.UtcNow,
            BotName = !string.IsNullOrEmpty(services.BotSessionService.BotName)
                ? services.BotSessionService.BotName
                : FallbackBotName,
            Version = services.Configuration.GetValue<string>(Constants.Environment.BotVersion) ?? UnknownVersion,
            UpTimeInSeconds = (int)(DateTime.UtcNow - services.BotSessionService.StartedAt).TotalSeconds
        };
    }

    /// <summary>
    /// Attempts to log basic statistics when main collection fails completely.
    /// </summary>
    private async Task LogFallbackStatisticsAsync(IServiceScopeFactory serviceScopeFactory, Exception originalException, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<StatisticsCollector>>();
            var statisticsRepository = scope.ServiceProvider.GetRequiredService<IBotStatisticsRepository>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var fallbackStats = new BotStatistics
            {
                Id = Guid.NewGuid().ToString(),
                CreatedDate = DateTime.UtcNow,
                BotName = FallbackBotName,
                Version = config.GetValue<string>(Constants.Environment.BotVersion) ?? UnknownVersion
            };

            await statisticsRepository.AddAsync(fallbackStats);
            await statisticsRepository.SaveChangesAsync();

            logger.LogWarning("Logged fallback statistics due to collection failure: {error}", originalException.Message);
        }
        catch (Exception fallbackEx)
        {
            // If even fallback fails, just log the error - don't throw
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<StatisticsCollector>>();
                logger.LogCritical(fallbackEx, "Critical: Both main and fallback statistics collection failed");
            }
            catch
            {
                // If we can't even log, there's nothing more we can do
            }
        }
    }

    #region Data Transfer Objects

    private sealed record DiscordMetrics
    {
        public int GuildCount { get; init; }
        public int ChannelCount { get; init; }
        public int UserCount { get; init; }
        public int ShardCount { get; init; }
        public int Latency { get; init; }
    }

    private sealed record CommandMetrics
    {
        public int TotalCommands { get; init; }
        public int FailedCommands { get; init; }
        public double AverageExecutionTime { get; init; }
    }

    private sealed record SystemMetrics
    {
        public float CpuUsage { get; init; }
        public float AvailableMemoryMB { get; init; }
        public float UsedMemoryMB { get; init; }
        public float UsedMemoryPercentage { get; init; }
        public int UptimeSeconds { get; init; }
    }

    private sealed record CacheMetrics
    {
        public int ItemCount { get; init; }
    }

    private sealed class ServiceContainer
    {
        public ILogger<StatisticsCollector> Logger { get; init; } = null!;
        public IMemoryCache MemoryCache { get; init; } = null!;
        public DiscordShardedClient DiscordClient { get; init; } = null!;
        public ICommandLogRepository CommandLogRepository { get; init; } = null!;
        public IBotStatisticsRepository StatisticsRepository { get; init; } = null!;
        public IConfiguration Configuration { get; init; } = null!;
        public BotSessionService BotSessionService { get; init; } = null!;
    }

    #endregion
}