namespace Amiquin.Core.Services.Chat;

public interface IPersonaChatService
{
    Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message);
    Task<string> ExchangeMessageAsync(ulong instanceId, string message);

    /// <summary>
    /// Manually triggers history optimization for a specific instance.
    /// This compacts older messages into summaries to reduce token usage.
    /// </summary>
    /// <param name="instanceId">The instance ID to optimize history for.</param>
    /// <returns>A tuple containing success status and a message describing the result.</returns>
    Task<(bool success, string message)> TriggerHistoryOptimizationAsync(ulong instanceId);
}