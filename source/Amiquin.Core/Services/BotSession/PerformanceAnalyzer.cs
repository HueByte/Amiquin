using System.Diagnostics;
using System.Runtime.InteropServices;
using Amiquin.Core.Utilities;

namespace Amiquin.Core.Services.BotSession
{
    public interface IPerformanceAnalyzer
    {
        Task<float> GetAvailableMemoryMBAsync();
        Task<float> GetCpuUsageAsync();
        Task<float> GetApplicationMemoryUsedMBAsync();
        Task<float> GetApplicationMemoryUsagePercentageAsync();
    }

    public class PerformanceAnalyzerLinux : IPerformanceAnalyzer
    {
        private ulong _prevIdleTime;
        private ulong _prevTotalTime;

        public async Task<float> GetCpuUsageAsync()
        {
            var cpuStats = await File.ReadAllLinesAsync("/proc/stat");
            var cpuLine = cpuStats[0]; // "cpu  3357 0 4313 1362393 ..."
            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts[0] != "cpu")
                throw new InvalidOperationException("Unexpected format in /proc/stat");

            // Parse the fields (user, nice, system, idle, iowait, irq, softirq, steal, guest, guest_nice)
            ulong user = ulong.Parse(parts[1]);
            ulong nice = ulong.Parse(parts[2]);
            ulong system = ulong.Parse(parts[3]);
            ulong idle = ulong.Parse(parts[4]);
            ulong iowait = ulong.Parse(parts[5]);
            ulong irq = ulong.Parse(parts[6]);
            ulong softirq = ulong.Parse(parts[7]);
            ulong steal = ulong.Parse(parts[8]);

            ulong idleAllTime = idle + iowait;
            ulong totalTime = user + nice + system + idle + iowait + irq + softirq + steal;

            ulong diffIdle = idleAllTime - _prevIdleTime;
            ulong diffTotal = totalTime - _prevTotalTime;

            _prevIdleTime = idleAllTime;
            _prevTotalTime = totalTime;

            if (diffTotal == 0) return 0;
            return 100f * (1.0f - ((float)diffIdle / diffTotal));
        }

        public async Task<float> GetAvailableMemoryMBAsync()
        {
            var memInfo = await File.ReadAllLinesAsync("/proc/meminfo");
            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemAvailable:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int kb))
                    {
                        return kb / 1024f;
                    }
                }
            }

            throw new InvalidOperationException("MemAvailable not found in /proc/meminfo");
        }

        public async Task<float> GetApplicationMemoryUsedMBAsync()
        {
            var memInfo = await File.ReadAllLinesAsync("/proc/meminfo");
            float totalMemory = 0;
            float availableMemory = 0;

            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int kb))
                    {
                        totalMemory = kb / 1024f;
                    }
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int kb))
                    {
                        availableMemory = kb / 1024f;
                    }
                }
            }

            if (totalMemory == 0 || availableMemory == 0)
            {
                throw new InvalidOperationException("Unable to determine memory usage from /proc/meminfo");
            }

            return totalMemory - availableMemory;
        }

        public async Task<float> GetApplicationMemoryUsagePercentageAsync()
        {
            var usedMemory = await GetApplicationMemoryUsedMBAsync();
            var memInfo = await File.ReadAllLinesAsync("/proc/meminfo");
            float totalMemory = 0;

            foreach (var line in memInfo)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int kb))
                    {
                        totalMemory = kb / 1024f;
                    }
                }
            }

            if (totalMemory == 0)
            {
                throw new InvalidOperationException("Unable to determine total memory from /proc/meminfo");
            }

            return (usedMemory / totalMemory) * 100f;
        }
    }

    public class PerformanceAnalyzerWindows : IPerformanceAnalyzer
    {
        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _memoryCounter;

        public PerformanceAnalyzerWindows()
        {
            if (GeneralUtilities.IsLinux())
            {
                throw new PlatformNotSupportedException("PerformanceAnalyzerWindows is not supported on Linux.");
            }

            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }

        public Task<float> GetCpuUsageAsync()
        {
            return Task.FromResult(_cpuCounter.NextValue());
        }

        public Task<float> GetAvailableMemoryMBAsync()
        {
            return Task.FromResult(_memoryCounter.NextValue());
        }

        public Task<float> GetApplicationMemoryUsedMBAsync()
        {
            var totalMemory = new PerformanceCounter("Memory", "Committed Bytes").NextValue() / (1024 * 1024);
            var availableMemory = _memoryCounter.NextValue();
            var usedMemory = totalMemory - availableMemory;

            if (totalMemory == 0)
            {
                throw new InvalidOperationException("Unable to determine memory usage on Windows.");
            }

            return Task.FromResult(usedMemory);
        }

        public async Task<float> GetApplicationMemoryUsagePercentageAsync()
        {
            var usedMemory = await GetApplicationMemoryUsedMBAsync();
            var totalMemory = new PerformanceCounter("Memory", "Committed Bytes").NextValue() / (1024 * 1024);

            if (totalMemory == 0)
            {
                throw new InvalidOperationException("Unable to determine total memory on Windows.");
            }

            return (usedMemory / totalMemory) * 100f;
        }
    }

    public static class PerformanceAnalyzerFactory
    {
        public static IPerformanceAnalyzer Create()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new PerformanceAnalyzerWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new PerformanceAnalyzerLinux();
            }
            else
            {
                throw new PlatformNotSupportedException("PerformanceAnalyzer is only supported on Windows and Linux.");
            }
        }
    }
}