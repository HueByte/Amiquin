using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public interface IMessageCacheService
{
    void ClearCache();
    Task<string?> GetPersonaCoreMessage();
    Task<string?> GetServerJoinMessage();
    List<ChatMessage>? GetChatMessages(ulong key);
    void SetChatMessages(ulong channelId, List<ChatMessage> messages);
    void ClearOldMessages(ulong channelId, int range);
    int GetChatMessageCount(ulong channelId);
}