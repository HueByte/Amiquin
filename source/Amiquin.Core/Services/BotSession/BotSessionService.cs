using Amiquin.Core.Options;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.BotSession;

public class BotSessionService
{
    public string BotName { get; private set; } = string.Empty;
    public string BotVersion { get; private set; } = string.Empty;
    public DateTime StartedAt { get; private set; }
    public int CurrentUpTimeInSeconds => (int)(DateTime.UtcNow - StartedAt).TotalSeconds;
    private readonly IPerformanceAnalyzer _performanceAnalyzer = PerformanceAnalyzerFactory.Create();
    public async Task<float> GetCurrentCpuUsageAsync() => await _performanceAnalyzer.GetCpuUsageAsync();
    public async Task<float> GetAvailableMemoryMBAsync() => await _performanceAnalyzer.GetAvailableMemoryMBAsync();
    public async Task<float> GetUsedMemoryMBAsync() => await _performanceAnalyzer.GetApplicationMemoryUsedMBAsync();
    public async Task<float> GetUsedMemoryPercentageAsync() => await _performanceAnalyzer.GetApplicationMemoryUsagePercentageAsync();

    public BotSessionService(IOptions<BotOptions> botOptions)
    {
        StartedAt = DateTime.UtcNow;
        var options = botOptions.Value;
        BotName = options.Name;
        BotVersion = options.Version;
    }
}