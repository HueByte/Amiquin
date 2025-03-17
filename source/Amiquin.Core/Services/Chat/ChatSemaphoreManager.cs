using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat;

public class ChatSemaphoreManager : IChatSemaphoreManager
{
    private readonly ILogger<ChatSemaphoreManager> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _semaphores = new();

    public ChatSemaphoreManager(ILogger<ChatSemaphoreManager> logger)
    {
        _logger = logger;
    }

    public SemaphoreSlim GetOrCreateSemaphore(ulong channelId)
    {
        return _semaphores.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
    }

    public async Task StartSemaphoreCleanupAsync()
    {
        while (true)
        {
            try
            {
                await StartSemaphoreCleanupAsyncInternal();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appeared during semaphore cleanup");
            }
        }
    }

    private async Task StartSemaphoreCleanupAsyncInternal()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            foreach (var channelId in _semaphores.Keys)
            {
                if (_semaphores.TryGetValue(channelId, out var semaphore) && semaphore.CurrentCount == 1)
                {
                    _semaphores.TryRemove(channelId, out _);
                }
            }
        }
    }
}