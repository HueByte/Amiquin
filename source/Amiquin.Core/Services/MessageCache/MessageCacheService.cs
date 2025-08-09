using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

/// <summary>
/// Service implementation for managing message caching operations.
/// Handles in-memory caching of chat messages, persona messages, and database operations for message persistence.
/// </summary>
public class MessageCacheService : IMessageCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IMessageRepository _messageRepository;
    private readonly int _messageFetchCount = Constants.MessageCacheDefaults.DefaultMessageFetchCount;

    /// <summary>
    /// Initializes a new instance of the MessageCacheService.
    /// </summary>
    /// <param name="memoryCache">Memory cache for storing messages temporarily.</param>
    /// <param name="messageRepository">Repository for database operations on messages.</param>
    /// <param name="botOptions">Bot configuration options.</param>
    /// <param name="configuration">Application configuration.</param>
    public MessageCacheService(IMemoryCache memoryCache, IMessageRepository messageRepository, IOptions<BotOptions> botOptions, IConfiguration configuration)
    {
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
        int messageFetchCount = configuration.GetValue<int>(Constants.Environment.MessageFetchCount);
        _messageFetchCount = messageFetchCount is not 0 ? messageFetchCount : botOptions.Value.MessageFetchCount;
    }

    /// <inheritdoc/>
    public void ClearMessageCache()
    {
        _memoryCache.Remove(Constants.CacheKeys.ComputedPersonaMessageKey);
        _memoryCache.Remove(Constants.CacheKeys.CorePersonaMessageKey);
        _memoryCache.Remove(Constants.CacheKeys.JoinMessageKey);
    }

    /// <inheritdoc/>
    public async Task<string?> GetPersonaCoreMessageAsync()
    {
        return await GetMessageAsync(Constants.CacheKeys.CorePersonaMessageKey);
    }

    /// <inheritdoc/>
    public async Task<string?> GetServerJoinMessage()
    {
        return await GetMessageAsync(Constants.CacheKeys.JoinMessageKey);
    }

    /// <inheritdoc/>
    public int GetChatMessageCount(ulong serverId)
    {
        if (_memoryCache.TryGetValue(serverId, out List<ChatMessage>? channelMessages))
        {
            return channelMessages?.Count ?? 0;
        }

        return 0;
    }

    /// <inheritdoc/>
    public async Task<List<ChatMessage>?> GetOrCreateChatMessagesAsync(ulong serverId)
    {
        return await _memoryCache.GetOrCreateAsync<List<ChatMessage>?>(serverId, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(Constants.MessageCacheDefaults.MemoryCacheExpirationDays);
            var messages = await _messageRepository.AsQueryable()
                .Where(x => x.ServerId == serverId)
                .OrderBy(x => x.CreatedAt)
                .TakeLast(_messageFetchCount) // Get the most recent messages
                .ToListAsync();

            return messages.Select(x =>
                    x.IsUser
                        ? (ChatMessage)ChatMessage.CreateUserMessage(x.Content)
                        : ChatMessage.CreateAssistantMessage(x.Content)
                ).ToList();
        });
    }

    /// <inheritdoc/>
    public async Task AddChatExchangeAsync(ulong instanceId, List<ChatMessage> messages, List<Message> modelMessages)
    {
        // Get existing messages or create new list
        List<ChatMessage> chatMessages;
        if (_memoryCache.TryGetValue(instanceId, out List<ChatMessage>? existingMessages) && existingMessages != null)
        {
            // Update the existing cached messages with the new ones
            existingMessages.AddRange(messages);
            chatMessages = existingMessages;
        }
        else
        {
            // Create new message list from the provided messages
            chatMessages = new List<ChatMessage>(messages);
        }

        // Update cache with the complete message list
        _memoryCache.Set(instanceId, chatMessages, TimeSpan.FromDays(Constants.MessageCacheDefaults.MemoryCacheExpirationDays));

        // Persist to database
        await _messageRepository.AddRangeAsync(modelMessages);
        await _messageRepository.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public void ClearOldMessages(ulong instanceId, int range)
    {
        if (_memoryCache.TryGetValue(instanceId, out List<ChatMessage>? serverMessages))
        {
            if (serverMessages is not null && serverMessages.Count > range)
            {
                serverMessages.RemoveRange(0, serverMessages.Count - range);
                _memoryCache.Set(instanceId, serverMessages, TimeSpan.FromDays(Constants.MessageCacheDefaults.MemoryCacheExpirationDays));
            }
        }
    }

    /// <inheritdoc/>
    public void ModifyMessage(string key, string message, int minutes = Constants.MessageCacheDefaults.DefaultModifyMessageTimeoutMinutes)
    {
        _memoryCache.Set(key, message, TimeSpan.FromMinutes(minutes));
    }

    private async Task<string?> GetMessageAsync(string key)
    {
        if (_memoryCache.TryGetValue(key, out string? message))
        {
            return message;
        }
        else if (File.Exists(Path.Join(Constants.Paths.Assets, $"{key}.md")))
        {
            message = await File.ReadAllTextAsync(Path.Join(Constants.Paths.Assets, $"{key}.md"));
            _memoryCache.Set(key, message, TimeSpan.FromMinutes(Constants.Timeouts.MessageCacheTimeoutMinutes));
            return message;
        }

        return null;
    }
}