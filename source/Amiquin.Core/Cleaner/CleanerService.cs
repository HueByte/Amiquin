using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Amiquin.Core.Cleaner;

/// <summary>
/// Enhanced service implementation for cleaning and maintenance operations.
/// Performs comprehensive cleanup of caches, temporary data, logs, and system resources.
/// </summary>
public class CleanerService : ICleanerService
{
    /// <summary>
    /// Gets or sets the frequency in seconds for running the cleanup job.
    /// Default is 1 hour (3600 seconds).
    /// </summary>
    public int FrequencyInSeconds { get; set; } = 3600;

    /// <summary>
    /// Runs the comprehensive cleanup operations asynchronously.
    /// </summary>
    /// <param name="serviceScopeFactory">Factory for creating service scopes.</param>
    /// <param name="cancellationToken">Cancellation token for stopping the operation.</param>
    public async Task RunAsync(IServiceScopeFactory serviceScopeFactory, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CleanerService>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        logger.LogInformation("Starting comprehensive system cleanup");
        var stopwatch = Stopwatch.StartNew();

        var cleanupTasks = new List<Task<(string Operation, bool Success, long ItemsProcessed)>>
        {
            CleanupMessageCacheAsync(scope, logger, cancellationToken),
            CleanupMemoryCacheAsync(scope, logger, cancellationToken),
            CleanupOldLogFilesAsync(configuration, logger, cancellationToken),
            CleanupTemporaryFilesAsync(logger, cancellationToken),
            ForceGarbageCollectionAsync(logger, cancellationToken)
        };

        var results = await Task.WhenAll(cleanupTasks);

        stopwatch.Stop();
        LogCleanupSummary(logger, results, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Clears message cache and related cached data.
    /// </summary>
    private static async Task<(string Operation, bool Success, long ItemsProcessed)> CleanupMessageCacheAsync(
        IServiceScope scope, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var messageCacheService = scope.ServiceProvider.GetRequiredService<IMessageCacheService>();
            logger.LogDebug("Clearing message cache");

            messageCacheService.ClearMessageCache();

            await Task.Delay(100, cancellationToken);
            return ("Message Cache", true, 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup message cache");
            return ("Message Cache", false, 0);
        }
    }

    /// <summary>
    /// Clears memory cache with detailed reporting.
    /// </summary>
    private static async Task<(string Operation, bool Success, long ItemsProcessed)> CleanupMemoryCacheAsync(
        IServiceScope scope, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();

            if (cache is MemoryCache memoryCache)
            {
                var initialCount = memoryCache.Count;
                logger.LogDebug("Clearing memory cache with {CacheCount} items", initialCount);

                // Log cache keys for debugging if enabled
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    var cacheKeys = string.Join(", ", memoryCache.Keys.Take(10));
                    logger.LogTrace("Sample cache keys: {CacheKeys}{More}",
                        cacheKeys,
                        memoryCache.Count > Constants.Limits.CacheDisplayThreshold ? "..." : "");
                }

                memoryCache.Clear();

                await Task.Delay(100, cancellationToken);
                return ("Memory Cache", true, initialCount);
            }

            return ("Memory Cache", true, 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup memory cache");
            return ("Memory Cache", false, 0);
        }
    }

    /// <summary>
    /// Cleans up old log files based on retention policy.
    /// </summary>
    private static async Task<(string Operation, bool Success, long ItemsProcessed)> CleanupOldLogFilesAsync(
        IConfiguration configuration, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var logsPath = configuration.GetValue<string>("DataPaths:Logs") ?? Constants.Paths.DefaultDataLogsPath;
            var logDirectory = Path.GetFullPath(logsPath);

            if (!Directory.Exists(logDirectory))
            {
                logger.LogDebug("Logs directory does not exist: {LogDirectory}", logDirectory);
                return ("Log Files", true, 0);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep logs for 30 days
            var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.AllDirectories)
                .Where(f => File.GetLastWriteTimeUtc(f) < cutoffDate)
                .ToList();

            var deletedCount = 0;
            foreach (var logFile in logFiles)
            {
                try
                {
                    File.Delete(logFile);
                    deletedCount++;
                    logger.LogTrace("Deleted old log file: {LogFile}", Path.GetFileName(logFile));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete log file: {LogFile}", logFile);
                }
            }

            if (deletedCount > 0)
            {
                logger.LogDebug("Deleted {DeletedCount} old log files older than {CutoffDate}",
                    deletedCount, cutoffDate.ToString("yyyy-MM-dd"));
            }

            await Task.Delay(100, cancellationToken);
            return ("Log Files", true, deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup old log files");
            return ("Log Files", false, 0);
        }
    }

    /// <summary>
    /// Cleans up temporary files and directories created by the application.
    /// </summary>
    private static async Task<(string Operation, bool Success, long ItemsProcessed)> CleanupTemporaryFilesAsync(
        ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var cleanedCount = 0;

            // Clean TTS output files
            var ttsOutputPath = Constants.Paths.TTSBaseOutputPath;
            if (Directory.Exists(ttsOutputPath))
            {
                var ttsFiles = Directory.GetFiles(ttsOutputPath, "*.wav", SearchOption.AllDirectories)
                    .Where(f => File.GetLastAccessTimeUtc(f) < DateTime.UtcNow.AddHours(-24))
                    .ToList();

                foreach (var ttsFile in ttsFiles)
                {
                    try
                    {
                        File.Delete(ttsFile);
                        cleanedCount++;
                        logger.LogTrace("Deleted old TTS file: {TTSFile}", Path.GetFileName(ttsFile));
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to delete TTS file: {TTSFile}", ttsFile);
                    }
                }
            }

            // Clean system temp files related to the application
            var appTempPath = Constants.Paths.ApplicationTempPath;
            if (Directory.Exists(appTempPath))
            {
                try
                {
                    Directory.Delete(appTempPath, true);
                    cleanedCount++;
                    logger.LogTrace("Deleted application temp directory: {AppTempPath}", appTempPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete app temp directory: {AppTempPath}", appTempPath);
                }
            }

            await Task.Delay(100, cancellationToken);
            return ("Temporary Files", true, cleanedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup temporary files");
            return ("Temporary Files", false, 0);
        }
    }

    /// <summary>
    /// Forces garbage collection to free up memory.
    /// </summary>
    private static async Task<(string Operation, bool Success, long ItemsProcessed)> ForceGarbageCollectionAsync(
        ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var memoryBefore = GC.GetTotalMemory(false);

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memoryAfter = GC.GetTotalMemory(false);
            var memoryFreed = memoryBefore - memoryAfter;

            logger.LogDebug("Garbage collection freed {MemoryFreed:N0} bytes ({MemoryFreedMB:F2} MB)",
                memoryFreed, memoryFreed / (1024.0 * 1024.0));

            await Task.Delay(100, cancellationToken);
            return ("Garbage Collection", true, memoryFreed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to perform garbage collection");
            return ("Garbage Collection", false, 0);
        }
    }

    /// <summary>
    /// Logs a comprehensive summary of all cleanup operations.
    /// </summary>
    private static void LogCleanupSummary(ILogger logger,
        (string Operation, bool Success, long ItemsProcessed)[] results,
        long totalTimeMs)
    {
        var summary = new StringBuilder();
        summary.AppendLine("=== Cleanup Summary ===");

        var successful = 0;
        var failed = 0;
        var totalItemsProcessed = 0L;

        foreach (var (operation, success, itemsProcessed) in results)
        {
            var status = success ? "✓" : "✗";
            summary.AppendLine($"{status} {operation}: {itemsProcessed:N0} items");

            if (success) successful++;
            else failed++;

            totalItemsProcessed += itemsProcessed;
        }

        summary.AppendLine($"Total: {successful} successful, {failed} failed");
        summary.AppendLine($"Items processed: {totalItemsProcessed:N0}");
        summary.AppendLine($"Duration: {totalTimeMs:N0}ms");

        // Log memory usage info
        var currentMemory = GC.GetTotalMemory(false);
        summary.AppendLine($"Current memory usage: {currentMemory / (1024.0 * 1024.0):F2} MB");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var process = Process.GetCurrentProcess();
                summary.AppendLine($"Working set: {process.WorkingSet64 / (1024.0 * 1024.0):F2} MB");
            }
            catch
            {
                // Ignore if we can't get process info
            }
        }

        logger.LogInformation(summary.ToString());
    }
}