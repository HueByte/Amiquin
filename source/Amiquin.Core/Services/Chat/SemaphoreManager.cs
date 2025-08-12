using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manages semaphores for controlling concurrent access to chat operations.
/// </summary>
public class SemaphoreManager : ISemaphoreManager
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// Gets or creates a semaphore for the specified instance ID.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <returns>A semaphore for the specified instance</returns>
    public SemaphoreSlim GetOrCreateInstanceSemaphore(string instanceId)
    {
        return _semaphores.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
    }
}