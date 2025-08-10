using Amiquin.Core;
using Amiquin.Core.Services.ActivitySession;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.ActivitySession;

public class ActivitySessionServiceTests
{
    private readonly Mock<ILogger<ActivitySessionService>> _mockLogger;
    private readonly Mock<IChatContextService> _mockChatContextService;
    private readonly Mock<IToggleService> _mockToggleService;
    private readonly Mock<DiscordShardedClient> _mockDiscordClient;
    private readonly ActivitySessionService _service;

    public ActivitySessionServiceTests()
    {
        _mockLogger = new Mock<ILogger<ActivitySessionService>>();
        _mockChatContextService = new Mock<IChatContextService>();
        _mockToggleService = new Mock<IToggleService>();
        _mockDiscordClient = new Mock<DiscordShardedClient>();
        // Setup Discord client with current user - using null is fine for testing
        _mockDiscordClient.Setup(c => c.CurrentUser).Returns((SocketSelfUser?)null);

        _service = new ActivitySessionService(
            _mockLogger.Object,
            _mockChatContextService.Object,
            _mockToggleService.Object,
            _mockDiscordClient.Object
        );
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenLiveJobDisabled_ReturnsFalse()
    {
        // Arrange
        var guildId = 12345UL;
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        Assert.False(result);
        _mockChatContextService.Verify(cs => cs.GetCurrentActivityLevel(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenNoContextMessages_ReturnsFalse()
    {
        // Arrange
        var guildId = 12345UL;
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(Array.Empty<string>());

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId, adjustFrequency =>
        {
            // Frequency callback should be called with 0.1 for low activity
            Assert.Equal(0.1, adjustFrequency);
        });

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenBotMentioned_ForcesEngagement()
    {
        // Arrange
        var guildId = 12345UL;
        var botUserId = 123456789UL;
        var contextMessages = new[]
        {
            "user1: hello everyone",
            "user2: hey Amiquin, how are you?",
            "user3: what's up",
            "user1: @Amiquin are you there?"
        };

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(1.0);
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        var mockGuild = new Mock<SocketGuild>();
        mockGuild.Setup(g => g.Name).Returns("TestGuild");
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync("Test response");

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        Assert.True(result);
        // Verify adaptive response was called (90% chance when mentioned)
        _mockChatContextService.Verify(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()), Times.Once);
    }

    [Theory]
    [InlineData("user1: hello Amiquin", true)]
    [InlineData("user1: hey @Amiquin", true)]
    [InlineData("user1: <@123456789> are you there?", true)]
    [InlineData("user1: hello everyone", false)]
    [InlineData("user1: talking about amiquin but not mentioning directly", true)] // Case insensitive
    public async Task ExecuteActivitySessionAsync_BotMentionDetection_WorksCorrectly(string message, bool shouldForceMention)
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[]
        {
            "user1: previous message",
            message,
            "user2: another message",
            "user3: latest message"
        };

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(0.5); // Low activity to test forced engagement
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        var mockGuild = new Mock<SocketGuild>();
        mockGuild.Setup(g => g.Name).Returns("TestGuild");
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync("Test adaptive response");
        _mockChatContextService
            .Setup(cs => cs.StartTopicAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync("Test topic response");

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        if (shouldForceMention)
        {
            Assert.True(result); // Should always engage when mentioned
        }
        // Note: When not mentioned, engagement depends on random chance, so we can't assert definitively
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WithRetryMechanism_RetriesOnFailure()
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[] { "user1: hello", "user2: world", "user3: test", "user4: message" };

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(2.0); // High activity
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        var mockGuild = new Mock<SocketGuild>();
        mockGuild.Setup(g => g.Name).Returns("TestGuild");
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        // Setup to fail first two attempts, succeed on third
        var callCount = 0;
        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount <= 2)
                    return Task.FromResult<string?>(null); // Simulate failure
                return Task.FromResult<string?>("Success on retry");
            });

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        Assert.True(result);
        Assert.Equal(3, callCount); // Should have retried 3 times total
        _mockChatContextService.Verify(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenMaxRetriesExceeded_ReturnsFalse()
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[] { "user1: hello", "user2: world", "user3: test", "user4: message" };

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(2.0); // High activity to ensure engagement attempt
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        var mockGuild = new Mock<SocketGuild>();
        mockGuild.Setup(g => g.Name).Returns("TestGuild");
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        // Setup to always return null (failure)
        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        Assert.False(result);
        // Should have tried maximum 3 times
        _mockChatContextService.Verify(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()), Times.Exactly(3));
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[] { "user1: test message" };
        var cts = new CancellationTokenSource();

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(2.0); // High activity to ensure engagement
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        var mockGuild = new Mock<SocketGuild>();
        mockGuild.Setup(g => g.Name).Returns("TestGuild");
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns(mockGuild.Object);

        // Setup to cancel during execution
        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .Returns(async () =>
            {
                cts.Cancel(); // Cancel during execution
                await Task.Delay(100, cts.Token); // This should throw
                return "Should not reach here";
            });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ExecuteActivitySessionAsync(guildId, cancellationToken: cts.Token));
    }
}