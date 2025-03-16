using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public class MessageCacheService : IMessageCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly string MESSAGES_BASE_PATH = Path.Join(AppContext.BaseDirectory, "Messages");

    public MessageCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public void ClearCache()
    {
        _memoryCache.Remove(Constants.PersonaMessageKey);
    }

    public async Task<string?> GetPersonaMessage()
    {
        return await GetMessageAsync(Constants.PersonaMessageKey);
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

    public async Task<List<ChatMessage>> GetChatMessages(ulong channelId)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            return channelMessages ?? await CreateFreshMessageSession(channelId);
        }

        return await CreateFreshMessageSession(channelId);
    }

    public async Task AddOrUpdateChatMessage(ulong channelId, ChatMessage message)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            if (channelMessages is null || channelMessages.Count == 0)
            {
                channelMessages = await CreateFreshMessageSession(channelId, message);
            }
            else
            {
                channelMessages.Add(message);
            }

            _memoryCache.Set(channelId, channelMessages, TimeSpan.FromDays(4));
        }
        else
        {
            var messages = await CreateFreshMessageSession(channelId, message);
            _memoryCache.Set(channelId, messages, TimeSpan.FromDays(4));
        }
    }

    public void ClearOldMessages(ulong channelId, int range)
    {
        if (_memoryCache.TryGetValue(channelId, out List<ChatMessage>? channelMessages))
        {
            if (channelMessages is not null && channelMessages.Count > range)
            {
                // Starting from 1 to not remove the developer message
                channelMessages.RemoveRange(1, channelMessages.Count - range);
                _memoryCache.Set(channelId, channelMessages, TimeSpan.FromDays(4));
            }
        }
    }

    private async Task<List<ChatMessage>> CreateFreshMessageSession(ulong channelId)
    {
        var devMessage = await GetMessageAsync(Constants.PersonaMessageKey);
        var messages = new List<ChatMessage> { ChatMessage.CreateDeveloperMessage(devMessage) };

        _memoryCache.Set(channelId, messages, TimeSpan.FromDays(4));

        return messages;
    }

    private async Task<List<ChatMessage>> CreateFreshMessageSession(ulong channelId, ChatMessage userMessage)
    {
        var devMessage = await GetMessageAsync(Constants.PersonaMessageKey);
        var messages = new List<ChatMessage> { ChatMessage.CreateDeveloperMessage(devMessage), userMessage };

        _memoryCache.Set(channelId, messages, TimeSpan.FromDays(4));

        return messages;
    }

    private async Task<string?> GetMessageAsync(string key)
    {
        if (_memoryCache.TryGetValue(key, out string? message))
        {
            return message;
        }
        else if (File.Exists(Path.Join(MESSAGES_BASE_PATH, $"{key}.md")))
        {
            message = await File.ReadAllTextAsync(Path.Join(MESSAGES_BASE_PATH, $"{key}.md"));
            _memoryCache.Set(key, message, TimeSpan.FromMinutes(5));
            return message;
        }

        return null;
    }
}