#if false // Complex mocking scenarios - these tests should be moved to integration tests
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using OpenAI.Chat;
using Xunit;

namespace Amiquin.Tests.Services;

public class MessageCacheServiceTests
{
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<IMessageRepository> _messageRepositoryMock;
    private readonly Mock<IOptions<BotOptions>> _botOptionsMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly MessageCacheService _sut; // System Under Test

    public MessageCacheServiceTests()
    {
        _memoryCacheMock = new Mock<IMemoryCache>();
        _messageRepositoryMock = new Mock<IMessageRepository>();
        _botOptionsMock = new Mock<IOptions<BotOptions>>();
        _configurationMock = new Mock<IConfiguration>();

        var botOptions = new BotOptions { MessageFetchCount = 40 };
        _botOptionsMock.Setup(x => x.Value).Returns(botOptions);

        var configSectionMock = new Mock<IConfigurationSection>();
        configSectionMock.Setup(x => x.Value).Returns("0");
        _configurationMock.Setup(x => x.GetSection(It.IsAny<string>())).Returns(configSectionMock.Object);

        _sut = new MessageCacheService(
            _memoryCacheMock.Object,
            _messageRepositoryMock.Object,
            _botOptionsMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public void ClearMessageCache_ShouldRemoveAllCachedMessages()
    {
        // Act
        _sut.ClearMessageCachce();

        // Assert
        _memoryCacheMock.Verify(x => x.Remove("computed_persona_message"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("core_persona_message"), Times.Once);
        _memoryCacheMock.Verify(x => x.Remove("join_message"), Times.Once);
    }

    [Fact]
    public async Task GetPersonaCoreMessageAsync_ShouldReturnCachedMessage()
    {
        // Arrange
        var expectedMessage = "This is the core persona message";
        object? value = expectedMessage;
        _memoryCacheMock.Setup(x => x.TryGetValue("core_persona_message", out value))
            .Returns(true);

        // Act
        var result = await _sut.GetPersonaCoreMessageAsync();

        // Assert
        Assert.Equal(expectedMessage, result);
    }

    [Fact]
    public async Task GetPersonaCoreMessageAsync_WithNoCache_ShouldReturnNull()
    {
        // Arrange
        object? value = null;
        _memoryCacheMock.Setup(x => x.TryGetValue("core_persona_message", out value))
            .Returns(false);

        // Act
        var result = await _sut.GetPersonaCoreMessageAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetServerJoinMessage_ShouldReturnCachedMessage()
    {
        // Arrange
        var expectedMessage = "Welcome to the server!";
        object? value = expectedMessage;
        _memoryCacheMock.Setup(x => x.TryGetValue("join_message", out value))
            .Returns(true);

        // Act
        var result = await _sut.GetServerJoinMessage();

        // Assert
        Assert.Equal(expectedMessage, result);
    }

    [Fact]
    public void GetChatMessageCount_WithCachedMessages_ShouldReturnCount()
    {
        // Arrange
        var serverId = 123456789UL;
        var cachedMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("Hello"),
            ChatMessage.CreateAssistantMessage("Hi there!")
        };
        object? value = cachedMessages;
        _memoryCacheMock.Setup(x => x.TryGetValue(serverId, out value))
            .Returns(true);

        // Act
        var result = _sut.GetChatMessageCount(serverId);

        // Assert
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetChatMessageCount_WithNoCachedMessages_ShouldReturnZero()
    {
        // Arrange
        var serverId = 123456789UL;
        object? value = null;
        _memoryCacheMock.Setup(x => x.TryGetValue(serverId, out value))
            .Returns(false);

        // Act
        var result = _sut.GetChatMessageCount(serverId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void GetChatMessageCount_WithNullCachedMessages_ShouldReturnZero()
    {
        // Arrange
        var serverId = 123456789UL;
        object? value = null;
        _memoryCacheMock.Setup(x => x.TryGetValue(serverId, out value))
            .Returns(true);

        // Act
        var result = _sut.GetChatMessageCount(serverId);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetOrCreateChatMessagesAsync_ShouldCreateAndCacheMessages()
    {
        // Arrange
        var serverId = 123456789UL;
        var dbMessages = new List<Message>
        {
            new Message { Id = "1", ServerId = serverId, Content = "User message", IsUser = true, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new Message { Id = "2", ServerId = serverId, Content = "Assistant response", IsUser = false, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };

        var mockCacheEntry = new Mock<ICacheEntry>();
        object? value = null;
        _memoryCacheMock.Setup(x => x.TryGetValue(serverId, out value)).Returns(false);
        _memoryCacheMock.Setup(x => x.CreateEntry(serverId)).Returns(mockCacheEntry.Object);

        _messageRepositoryMock.Setup(x => x.AsQueryable())
            .Returns(dbMessages.AsQueryable());

        // Act
        var result = await _sut.GetOrCreateChatMessagesAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("User message", result[0].Content.ToString());
        Assert.Equal("Assistant response", result[1].Content.ToString());
    }

    [Fact]
    public void ClearOldMessages_WithCachedMessages_ShouldKeepOnlyRecentMessages()
    {
        // Arrange
        var instanceId = 123456789UL;
        var range = 3;
        var cachedMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("Message 1"),
            ChatMessage.CreateUserMessage("Message 2"),
            ChatMessage.CreateUserMessage("Message 3"),
            ChatMessage.CreateUserMessage("Message 4"),
            ChatMessage.CreateUserMessage("Message 5")
        };

        object? value = cachedMessages;
        _memoryCacheMock.Setup(x => x.TryGetValue(instanceId, out value))
            .Returns(true);

        var mockCacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock.Setup(x => x.CreateEntry(instanceId)).Returns(mockCacheEntry.Object);

        // Act
        _sut.ClearOldMessages(instanceId, range);

        // Assert
        _memoryCacheMock.Verify(m => m.Set(instanceId, It.Is<List<ChatMessage>>(list => list.Count == range), It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public void ClearOldMessages_WithNoCachedMessages_ShouldNotThrow()
    {
        // Arrange
        var instanceId = 123456789UL;
        var range = 3;
        object? value = null;
        _memoryCacheMock.Setup(x => x.TryGetValue(instanceId, out value))
            .Returns(false);

        // Act & Assert - Should not throw
        _sut.ClearOldMessages(instanceId, range);
    }

    [Fact]
    public async Task AddChatExchangeAsync_WithExistingCache_ShouldReplaceMessages()
    {
        // Arrange
        var instanceId = 123456789UL;
        var existingMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("Old message 1"),
            ChatMessage.CreateUserMessage("Old message 2")
        };
        var newMessages = new List<ChatMessage>
        {
            ChatMessage.CreateUserMessage("New message 1"),
            ChatMessage.CreateAssistantMessage("New response 1")
        };
        var modelMessages = new List<Message>
        {
            new Message { Content = "New message 1", IsUser = true },
            new Message { Content = "New response 1", IsUser = false }
        };

        object? value = existingMessages;
        _memoryCacheMock.Setup(x => x.TryGetValue(instanceId, out value))
            .Returns(true);

        var mockCacheEntry = new Mock<ICacheEntry>();
        _memoryCacheMock.Setup(x => x.CreateEntry(instanceId)).Returns(mockCacheEntry.Object);

        _messageRepositoryMock.Setup(x => x.AddRangeAsync(It.IsAny<IEnumerable<Message>>()))
            .ReturnsAsync(true);
        _messageRepositoryMock.Setup(x => x.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AddChatExchangeAsync(instanceId, newMessages, modelMessages);

        // Assert
        _messageRepositoryMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<Message>>(msgs =>
            msgs.Count() == 2 &&
            msgs.Any(m => m.Content == "New message 1") &&
            msgs.Any(m => m.Content == "New response 1")
        )), Times.Once);
        _messageRepositoryMock.Verify(x => x.SaveChangesAsync(), Times.Once);
    }
}
#endif