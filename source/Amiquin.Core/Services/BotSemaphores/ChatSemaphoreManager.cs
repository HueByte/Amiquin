using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat;

public class ChatSemaphoreManager : IChatSemaphoreManager
{
    private readonly ILogger<ChatSemaphoreManager> _logger;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _textSemaphores = new();
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _voiceSemaphores = new();

    public ChatSemaphoreManager(ILogger<ChatSemaphoreManager> logger)
    {
        _logger = logger;
    }

    public SemaphoreSlim GetOrCreateTextSemaphore(ulong channelId)
    {
        _logger.LogInformation("Getting or creating text semaphore for channel {ChannelId}", channelId);
        return _textSemaphores.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
    }

    public SemaphoreSlim GetOrCreateVoiceSemaphore(ulong serverId)
    {
        _logger.LogInformation("Getting or creating voice semaphore for channel {ChannelId}", serverId);
        return _voiceSemaphores.GetOrAdd(serverId, _ => new SemaphoreSlim(1, 1));
    }

    public async Task StartSemaphoreCleanupAsync()
    {
        while (true)
        {
            try
            {
                _logger.LogInformation("Starting semaphore cleanup");
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
            foreach (var channelId in _textSemaphores.Keys)
            {
                if (_textSemaphores.TryGetValue(channelId, out var semaphore) && semaphore.CurrentCount == 1)
                {
                    _textSemaphores.TryRemove(channelId, out _);
                }
            }
            foreach (var channelId in _voiceSemaphores.Keys)
            {
                if (_textSemaphores.TryGetValue(channelId, out var semaphore) && semaphore.CurrentCount == 1)
                {
                    _textSemaphores.TryRemove(channelId, out _);
                }
            }
        }
    }
}