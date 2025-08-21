using Amiquin.Core.Models;
using OpenAI.Chat;

namespace Amiquin.Core.Services.MessageCache;

/// <summary>
/// Service interface for managing message caching operations.
/// Provides methods for caching chat messages, persona messages, and managing message history.
/// </summary>
public interface IMessageCacheService
{
    /// <summary>
    /// Clears all cached messages.
    /// </summary>
    void ClearMessageCache();

    /// <summary>
    /// Retrieves the core system message from cache.
    /// </summary>
    /// <returns>The system core message if available; otherwise, null.</returns>
    Task<string?> GetSystemCoreMessageAsync();

    /// <summary>
    /// Retrieves the server join message from cache.
    /// </summary>
    /// <returns>The server join message if available; otherwise, null.</returns>
    Task<string?> GetServerJoinMessage();

    /// <summary>
    /// Retrieves existing chat messages for a server or creates a new message collection.
    /// </summary>
    /// <param name="serverId">The Discord server ID to get or create messages for.</param>
    /// <returns>A list of chat messages for the specified server.</returns>
    Task<List<ChatMessage>?> GetOrCreateChatMessagesAsync(ulong serverId);

    /// <summary>
    /// Adds a chat exchange to the message cache for a specific server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to add the exchange for.</param>
    /// <param name="messages">The chat messages to add.</param>
    /// <param name="modelMessages">The corresponding model messages to store.</param>
    Task AddChatExchangeAsync(ulong serverId, List<ChatMessage> messages, List<Message> modelMessages);

    /// <summary>
    /// Clears old messages for a specific instance to manage memory usage.
    /// </summary>
    /// <param name="instanceId">The instance ID to clear messages for.</param>
    /// <param name="range">The number of messages to keep from the most recent.</param>
    void ClearOldMessages(ulong instanceId, int range);

    /// <summary>
    /// Gets the count of cached chat messages for a specific instance.
    /// </summary>
    /// <param name="instanceId">The instance ID to get the message count for.</param>
    /// <returns>The number of cached messages for the instance.</returns>
    int GetChatMessageCount(ulong instanceId);

    /// <summary>
    /// Clears the cached chat messages for a specific server.
    /// This should be called when switching sessions to ensure fresh message history.
    /// </summary>
    /// <param name="serverId">The Discord server ID to clear cached messages for.</param>
    void ClearServerMessageCache(ulong serverId);

    /// <summary>
    /// Modifies or sets a message in the cache with a specified expiration time.
    /// </summary>
    /// <param name="key">The cache key for the message.</param>
    /// <param name="message">The message content to cache.</param>
    /// <param name="minutes">The expiration time in minutes. Defaults to 30 minutes if not specified.</param>
    void ModifyMessage(string key, string message, int minutes = 30);
}