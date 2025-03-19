namespace Amiquin.Core.Services.Chat;

public interface IChatSemaphoreManager
{
    SemaphoreSlim GetOrCreateVoiceSemaphore(ulong instanceId);
    SemaphoreSlim GetOrCreateInstanceSemaphore(ulong instanceId);
}
