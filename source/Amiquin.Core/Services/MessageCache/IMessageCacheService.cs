using Amiquin.Core.Models;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public interface IMessageCacheService
{
    void ClearCache();
    Task<string?> GetPersonaCoreMessage();
    Task<string?> GetServerJoinMessage();
    Task<List<ChatMessage>?> GetOrCreateChatMessagesAsync(ulong channelId);
    Task AddChatExchange(ulong channelId, List<ChatMessage> messages, List<Message> modelMessages);
    void ClearOldMessages(ulong channelId, int range);
    int GetChatMessageCount(ulong channelId);
}