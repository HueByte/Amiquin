using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

public class BotStatistics : DbModel<string>
{
    public int TotalServersCount { get; set; }
    public int TotalChannelsCount { get; set; }
    public int TotalUsersCount { get; set; }
    public int TotalCommandsCount { get; set; }
    public int TotalErrorsCount { get; set; }
    public double AverageCommandExecutionTimeInMs { get; set; }
    public int Latency { get; set; }
    public int ShardCount { get; set; }
    public int UpTimeInSeconds { get; set; }
    public int CacheItems { get; set; }
    public float AvailableMemoryMB { get; set; }
    public float UsedMemoryMB { get; set; }
    public float UsedMemoryPercentage { get; set; }
    public double CpuUsage { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? Version { get; set; }
    public string BotName { get; set; } = "Amiquin";
}