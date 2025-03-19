namespace Amiquin.Core.Services.Chat;

public interface IChatSemaphoreManager
{
    SemaphoreSlim GetOrCreateInstanceSemaphore(ulong instanceId);
}
