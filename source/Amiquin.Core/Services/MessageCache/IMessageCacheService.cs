using Amiquin.Core.Models;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public interface IMessageCacheService
{
    void ClearMessageCachce();
    Task<string?> GetPersonaCoreMessageAsync();
    Task<string?> GetServerJoinMessage();
    Task<List<ChatMessage>?> GetOrCreateChatMessagesAsync(ulong instanceId);
    Task AddChatExchangeAsync(ulong instanceId, List<ChatMessage> messages, List<Message> modelMessages);
    void ClearOldMessages(ulong instanceId, int range);
    int GetChatMessageCount(ulong instanceId);
}