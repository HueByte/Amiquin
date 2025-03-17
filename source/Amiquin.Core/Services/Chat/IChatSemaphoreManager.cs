namespace Amiquin.Core.Services.Chat;

public interface IChatSemaphoreManager
{
    SemaphoreSlim GetOrCreateSemaphore(ulong channelId);
}
