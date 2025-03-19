using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

public class MessageCacheService : IMessageCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IMessageRepository _messageRepository;
    private const int MEMORY_CACHE_EXPIRATION = 5;

    public MessageCacheService(IMemoryCache memoryCache, IMessageRepository messageRepository)
    {
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
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

    public async Task<List<ChatMessage>?> GetOrCreateChatMessagesAsync(ulong instanceId)
    {
        return await _memoryCache.GetOrCreateAsync<List<ChatMessage>?>(instanceId, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION);
            var messages = await _messageRepository.AsQueryable()
                .Where(x => x.InstanceId == instanceId)
                .OrderBy(x => x.CreatedAt)
                .Take(40)
                .ToListAsync();

            return messages.Select(x =>
                    x.IsUser
                        ? (ChatMessage)ChatMessage.CreateUserMessage(x.Content)
                        : ChatMessage.CreateAssistantMessage(x.Content)
                ).ToList();
        });
    }

    public async Task AddChatExchangeAsync(ulong instanceId, List<ChatMessage> messages, List<Message> modelMessages)
    {
        List<ChatMessage>? chatMessages;
        if (_memoryCache.TryGetValue(instanceId, out chatMessages))
        {
            if (chatMessages?.Count > 0)
            {
                chatMessages.Clear();
            }

            chatMessages = chatMessages ?? messages;
            chatMessages.AddRange(messages);

            _memoryCache.Set(instanceId, chatMessages, TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION));
        }
        else
        {
            chatMessages = [.. messages];
            _memoryCache.Set(instanceId, messages, TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION));
        }

        await _messageRepository.AddRangeAsync(modelMessages);
        await _messageRepository.SaveChangesAsync();
    }

    public void ClearOldMessages(ulong instanceId, int range)
    {
        if (_memoryCache.TryGetValue(instanceId, out List<ChatMessage>? channelMessages))
        {
            if (channelMessages is not null && channelMessages.Count > range)
            {
                channelMessages.RemoveRange(0, channelMessages.Count - range);
                _memoryCache.Set(instanceId, channelMessages, TimeSpan.FromDays(MEMORY_CACHE_EXPIRATION));
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