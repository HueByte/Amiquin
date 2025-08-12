using Amiquin.Core.Models;
using Amiquin.Core.Services.MessageCache;
using Amiquin.IntegrationTests.Fixtures;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;

namespace Amiquin.IntegrationTests.Services;

public class MessageCacheServiceIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly IMessageCacheService _messageCacheService;

    public MessageCacheServiceIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _messageCacheService = _fixture.ServiceProvider.GetRequiredService<IMessageCacheService>();
    }

    [Fact]
    public async Task GetOrCreateChatMessagesAsync_WithExistingMessages_ShouldReturnMessagesFromDatabase()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);

        // Create server meta first
        var serverMetaService = _fixture.ServiceProvider.GetRequiredService<Core.Services.Meta.IServerMetaService>();
        await serverMetaService.CreateServerMetaAsync(serverId, "Test Server for Messages");

        // Add messages directly to database
        var messages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Content = "Hello from user",
                IsUser = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Content = "Hello from assistant",
                IsUser = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-4)
            },
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Content = "Another user message",
                IsUser = true,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            }
        };

        _fixture.DbContext.Messages.AddRange(messages);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        var result = await _messageCacheService.GetOrCreateChatMessagesAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Verify the messages are converted correctly
        var userMessages = result.OfType<UserChatMessage>().ToList();
        var assistantMessages = result.OfType<AssistantChatMessage>().ToList();

        Assert.Equal(2, userMessages.Count);
        Assert.Single(assistantMessages);

        // Verify content
        Assert.Contains(userMessages, m => m.Content[0].Text == "Hello from user");
        Assert.Contains(userMessages, m => m.Content[0].Text == "Another user message");
        Assert.Contains(assistantMessages, m => m.Content[0].Text == "Hello from assistant");
    }

    [Fact]
    public async Task GetOrCreateChatMessagesAsync_WithNoExistingMessages_ShouldReturnEmptyList()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = 999999999UL; // Non-existent server

        // Act
        var result = await _messageCacheService.GetOrCreateChatMessagesAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddChatExchangeAsync_ShouldPersistMessagesToDatabase()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var instanceId = 123456789UL;

        var chatMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("User question"),
            ChatMessage.CreateAssistantMessage("Assistant response")
        };

        var modelMessages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = instanceId,
                Content = "User question",
                IsUser = true,
                CreatedAt = DateTime.UtcNow
            },
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = instanceId,
                Content = "Assistant response",
                IsUser = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        await _messageCacheService.AddChatExchangeAsync(instanceId, chatMessages, modelMessages);

        // Assert
        // Verify messages were saved to database
        var savedMessages = _fixture.DbContext.Messages
            .Where(m => m.ServerId == instanceId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        Assert.Equal(2, savedMessages.Count);
        Assert.Equal("User question", savedMessages[0].Content);
        Assert.True(savedMessages[0].IsUser);
        Assert.Equal("Assistant response", savedMessages[1].Content);
        Assert.False(savedMessages[1].IsUser);
    }

    [Fact]
    public void GetChatMessageCount_WithCachedMessages_ShouldReturnCorrectCount()
    {
        // Arrange
        var serverId = 123456789UL;

        // First, cache some messages
        var chatMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("Message 1"),
            ChatMessage.CreateUserMessage("Message 2"),
            ChatMessage.CreateAssistantMessage("Response 1")
        };

        // We need to manually add to cache since GetOrCreateChatMessagesAsync uses the cache
        var memoryCache = _fixture.ServiceProvider.GetRequiredService<IMemoryCache>();
        memoryCache.Set(serverId, chatMessages, TimeSpan.FromDays(5));

        // Act
        var result = _messageCacheService.GetChatMessageCount(serverId);

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public void GetChatMessageCount_WithNoCachedMessages_ShouldReturnZero()
    {
        // Arrange
        var serverId = 999999999UL; // Non-cached server

        // Act
        var result = _messageCacheService.GetChatMessageCount(serverId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void ClearOldMessages_ShouldRemoveOldestMessagesFromCache()
    {
        // Arrange
        var instanceId = 123456789UL;
        var range = 2;

        var chatMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("Message 1"),
            ChatMessage.CreateUserMessage("Message 2"),
            ChatMessage.CreateUserMessage("Message 3"),
            ChatMessage.CreateUserMessage("Message 4")
        };

        var memoryCache = _fixture.ServiceProvider.GetRequiredService<IMemoryCache>();
        memoryCache.Set(instanceId, chatMessages, TimeSpan.FromDays(5));

        // Verify initial count
        var initialCount = _messageCacheService.GetChatMessageCount(instanceId);
        Assert.Equal(4, initialCount);

        // Act
        _messageCacheService.ClearOldMessages(instanceId, range);

        // Assert
        var finalCount = _messageCacheService.GetChatMessageCount(instanceId);
        Assert.Equal(range, finalCount);

        // Verify the remaining messages are the most recent ones
        if (memoryCache.TryGetValue(instanceId, out object? remainingMessagesObj) && remainingMessagesObj is List<ChatMessage> remainingMessages)
        {
            Assert.NotNull(remainingMessages);
            Assert.Equal(2, remainingMessages.Count);
            // The last 2 messages should remain (Message 3 and Message 4)
            var userMessage1 = remainingMessages[0] as UserChatMessage;
            var userMessage2 = remainingMessages[1] as UserChatMessage;
            Assert.Equal("Message 3", userMessage1?.Content[0].Text);
            Assert.Equal("Message 4", userMessage2?.Content[0].Text);
        }
    }

    [Fact]
    public void ClearMessageCache_ShouldRemoveSpecificCacheKeys()
    {
        // Arrange
        var memoryCache = _fixture.ServiceProvider.GetRequiredService<IMemoryCache>();

        // First clear any existing cache entries that might interfere with the test
        _messageCacheService.ClearMessageCache();

        // Add some test data to cache using correct constant keys
        memoryCache.Set(Core.Constants.CacheKeys.ComputedPersonaMessageKey, "test persona", TimeSpan.FromHours(1));
        memoryCache.Set(Core.Constants.CacheKeys.CorePersonaMessageKey, "test core", TimeSpan.FromHours(1));
        memoryCache.Set(Core.Constants.CacheKeys.JoinMessageKey, "test join", TimeSpan.FromHours(1));
        memoryCache.Set("other_cache_key", "should remain", TimeSpan.FromHours(1));

        // Act
        _messageCacheService.ClearMessageCache();

        // Assert
        // The specific keys should be removed
        Assert.False(memoryCache.TryGetValue(Core.Constants.CacheKeys.ComputedPersonaMessageKey, out _));
        Assert.False(memoryCache.TryGetValue(Core.Constants.CacheKeys.CorePersonaMessageKey, out _));
        Assert.False(memoryCache.TryGetValue(Core.Constants.CacheKeys.JoinMessageKey, out _));

        // Other keys should remain
        Assert.True(memoryCache.TryGetValue("other_cache_key", out var value));
        Assert.Equal("should remain", value);
    }

    [Fact]
    public void ModifyMessage_ShouldUpdateCacheWithNewValue()
    {
        // Arrange
        var key = "test_modify_key";
        var message = "Modified message content";
        var minutes = 60;

        var memoryCache = _fixture.ServiceProvider.GetRequiredService<IMemoryCache>();

        // Act
        _messageCacheService.ModifyMessage(key, message, minutes);

        // Assert
        Assert.True(memoryCache.TryGetValue(key, out var cachedValue));
        Assert.Equal(message, cachedValue);
    }

    [Fact]
    public async Task MessagePersistence_ShouldWorkAcrossServiceInstances()
    {
        // Arrange
        await _fixture.CleanupAsync();
        var serverId = (ulong)Random.Shared.Next(100000000, 999999999);

        // Create server meta first
        var serverMetaService = _fixture.ServiceProvider.GetRequiredService<Core.Services.Meta.IServerMetaService>();
        await serverMetaService.CreateServerMetaAsync(serverId, "Test Server for Persistence");

        // Add messages using first service instance
        var modelMessages = new List<Message>
        {
            new Message
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = serverId,
                Content = "Persistent message",
                IsUser = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _messageCacheService.AddChatExchangeAsync(serverId, new List<ChatMessage>(), modelMessages);

        // Ensure database context is saved and changes are committed
        await _fixture.DbContext.SaveChangesAsync();

        // Act - Clear cache to force database retrieval and test persistence
        var memoryCache = _fixture.ServiceProvider.GetRequiredService<IMemoryCache>();
        memoryCache.Remove(serverId);

        // Create new service instance and retrieve messages from database
        var newServiceInstance = _fixture.ServiceProvider.GetRequiredService<IMessageCacheService>();
        var retrievedMessages = await newServiceInstance.GetOrCreateChatMessagesAsync(serverId);

        // Assert
        Assert.NotNull(retrievedMessages);
        Assert.Single(retrievedMessages);

        var userMessage = retrievedMessages[0] as UserChatMessage;
        Assert.NotNull(userMessage);
        Assert.Equal("Persistent message", userMessage.Content[0].Text);
    }
}