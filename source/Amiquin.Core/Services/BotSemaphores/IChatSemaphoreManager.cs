namespace Amiquin.Core.Services.Chat;

public interface IChatSemaphoreManager
{
    SemaphoreSlim GetOrCreateTextSemaphore(ulong channelId);
    SemaphoreSlim GetOrCreateVoiceSemaphore(ulong serverId);
    Task StartSemaphoreCleanupAsync();
}
