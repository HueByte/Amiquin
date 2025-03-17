namespace Amiquin.Core.Services.Chat;

public interface IPersonaChatService
{
    Task<string> ChatAsync(ulong channelId, ulong userId, ulong botId, string message);
    Task<string> ExchangeMessageAsync(string message);
}
