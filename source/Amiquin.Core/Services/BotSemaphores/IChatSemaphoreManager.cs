namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manager interface for handling semaphore-based synchronization for chat and voice operations.
/// Provides methods for creating and managing semaphores to prevent concurrent operations.
/// </summary>
public interface IChatSemaphoreManager
{
    /// <summary>
    /// Gets or creates a semaphore for voice operations for the specified instance.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance.</param>
    /// <returns>A semaphore for controlling voice operation concurrency.</returns>
    SemaphoreSlim GetOrCreateVoiceSemaphore(ulong instanceId);

    /// <summary>
    /// Gets or creates a semaphore for general instance operations for the specified instance.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the instance.</param>
    /// <returns>A semaphore for controlling instance operation concurrency.</returns>
    SemaphoreSlim GetOrCreateInstanceSemaphore(ulong instanceId);
}
