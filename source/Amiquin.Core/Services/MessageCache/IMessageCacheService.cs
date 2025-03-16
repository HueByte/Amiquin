using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public interface IMessageCacheService
{
    void ClearCache();
    Task<string?> GetPersonaMessage();
    Task<string?> GetServerJoinMessage();
    Task<List<ChatMessage>> GetChatMessages(ulong key);
    Task AddOrUpdateChatMessage(ulong channelId, ChatMessage message);
    void ClearOldMessages(ulong channelId, int range);
    int GetChatMessageCount(ulong channelId);
}