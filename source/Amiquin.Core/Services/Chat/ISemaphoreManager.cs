namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manages semaphores for controlling concurrent access to chat operations.
/// </summary>
public interface ISemaphoreManager
{
    /// <summary>
    /// Gets or creates a semaphore for the specified instance ID.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance</param>
    /// <returns>A semaphore for the specified instance</returns>
    SemaphoreSlim GetOrCreateInstanceSemaphore(string instanceId);
}