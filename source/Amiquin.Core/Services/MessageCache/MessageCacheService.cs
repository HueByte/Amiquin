using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public class MessageCacheService : IMessageCacheService
{
    private readonly IMemoryCache _memoryCache;
    private const int MEMORY_CACHE_EXPIRATION = 5;

    public MessageCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public void ClearCache()
    {
        _memoryCache.Remove(Constants.ComputedPersonaMessageKey);
        _memoryCache.Remove(Constants.CorePersonaMessageKey);
        _memoryCache.Remove(Constants.JoinMessageKey);
    }

    public async Task<string?> GetPersonaCoreMessage()
    {
        return await GetMessageAsync(Constants.CorePersonaMessageKey);
    }

    public async Task<string?> GetServerJoinMessage()
    {
        return await GetMessageAsync(Constants.JoinMessageKey);
    }

    public int GetChatMessageCount(ulong channelId)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            return channelMessages?.Count ?? 0;
        }

        return 0;
    }

    public List<ChatMessage>? GetChatMessages(ulong channelId)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            return channelMessages;
        }

        return null;
    }

    public void SetChatMessages(ulong channelId, List<ChatMessage> messages)
    {
        _memoryCache.Set(channelId, messages, TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION));
    }

    public void ClearOldMessages(ulong channelId, int range)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            if (channelMessages is not null && channelMessages.Count > range)
            {
                // Starting from 1 to not remove the developer message
                channelMessages.RemoveRange(1, channelMessages.Count - range);
                _memoryCache.Set(channelId, channelMessages, TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION));
            }
        }
    }

    public void ModifyMessage(string key, string message, int minutes = 30)
    {
        _memoryCache.Set(key, message, TimeSpan.FromMinutes(minutes));
    }

    private async Task<string?> GetMessageAsync(string key)
    {
        if (_memoryCache.TryGetValue(key, out string? message))
        {
            return message;
        }
        else if (File.Exists(Path.Join(Constants.MessageBasePath, $"{key}.md")))
        {
            message = await File.ReadAllTextAsync(Path.Join(Constants.MessageBasePath, $"{key}.md"));
            _memoryCache.Set(key, message, TimeSpan.FromMinutes(5));
            return message;
        }

        return null;
    }
}