namespace Amiquin.Core.Services.Chat;

public interface IChatService
{
    Task<string> ChatAsync(ulong channelId, ulong userId, string message);
}