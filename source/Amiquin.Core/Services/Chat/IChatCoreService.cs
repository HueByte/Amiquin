using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

public interface IChatCoreService
{
    Task<string> ChatAsync(ulong channelId, ulong userId, ulong botId, string message, ChatMessage? personaChatMessage = null);
    Task<string> ExchangeMessageAsync(string message, ChatMessage? developerPersonaChatMessage = null, int tokenLimit = 1200);
}