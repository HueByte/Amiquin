using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Manager implementation for handling semaphore-based synchronization for chat and voice operations.
/// Uses concurrent dictionaries to manage semaphores per instance, ensuring thread-safe operations.
/// </summary>
public class ChatSemaphoreManager : IChatSemaphoreManager
{
    private readonly ILogger<ChatSemaphoreManager> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _personaSemaphore = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _voiceSemaphore = new();

    /// <summary>
    /// Initializes a new instance of the ChatSemaphoreManager.
    /// </summary>
    /// <param name="logger">Logger instance for recording manager operations.</param>
    public ChatSemaphoreManager(ILogger<ChatSemaphoreManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public SemaphoreSlim GetOrCreateVoiceSemaphore(ulong instanceId)
    {
        return _voiceSemaphore.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
    }

    /// <inheritdoc/>
    public SemaphoreSlim GetOrCreateInstanceSemaphore(ulong instanceId)
    {
        return _personaSemaphore.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
    }
}