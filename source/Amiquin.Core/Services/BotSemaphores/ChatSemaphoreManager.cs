using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat;

public class ChatSemaphoreManager : IChatSemaphoreManager
{
    private readonly ILogger<ChatSemaphoreManager> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _personaSemaphore = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _voiceSemaphore = new();

    public ChatSemaphoreManager(ILogger<ChatSemaphoreManager> logger)
    {
        _logger = logger;
    }

    public SemaphoreSlim GetOrCreateVoiceSemaphore(ulong instanceId)
    {
        _logger.LogInformation("Getting or creating voice semaphore for persona {PersonaId}", instanceId);
        return _voiceSemaphore.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
    }

    public SemaphoreSlim GetOrCreateInstanceSemaphore(ulong instanceId)
    {
        _logger.LogInformation("Getting or creating persona semaphore for persona {PersonaId}", instanceId);
        return _personaSemaphore.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
    }
}