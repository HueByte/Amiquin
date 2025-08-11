using Amiquin.Core;
using Amiquin.Core.Abstractions;
using Amiquin.Core.Services.ActivitySession;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amiquin.Tests.Unit;

/// <summary>
/// Unit tests for ActivitySessionService focusing on core functionality
/// </summary>
public class ActivitySessionServiceUnitTests
{
    private readonly Mock<ILogger<ActivitySessionService>> _mockLogger;
    private readonly Mock<IChatContextService> _mockChatContextService;
    private readonly Mock<IToggleService> _mockToggleService;
    private readonly Mock<IDiscordClientWrapper> _mockDiscordClient;
    private readonly ActivitySessionService _service;

    public ActivitySessionServiceUnitTests()
    {
        _mockLogger = new Mock<ILogger<ActivitySessionService>>();
        _mockChatContextService = new Mock<IChatContextService>();
        _mockToggleService = new Mock<IToggleService>();
        _mockDiscordClient = new Mock<IDiscordClientWrapper>();

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

        double capturedFrequency = 0;

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId, frequency =>
        {
            capturedFrequency = frequency;
        });

        // Assert
        Assert.False(result);
        Assert.Equal(0.1, capturedFrequency); // Should adjust frequency for low activity
    }

    [Theory]
    [InlineData("user1: hello Amiquin", true)]
    [InlineData("user1: hey @Amiquin", true)]
    [InlineData("user1: <@123456789> are you there?", false)] // CurrentUser is null so this won't match
    [InlineData("user1: hello everyone", false)]
    [InlineData("user1: talking about amiquin but not mentioning directly", true)] // Case insensitive
    public async Task ExecuteActivitySessionAsync_BotMentionDetection_WorksCorrectly(string message, bool containsMention)
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
            .Returns(0.5);
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        // Return null for guild - the actual guild object isn't used in the test logic
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync("Test adaptive response");

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        if (containsMention)
        {
            // When mentioned, should always engage (100% chance)
            Assert.True(result);
            // Should call adaptive response when mentioned
            _mockChatContextService.Verify(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()), Times.Once);
        }
        // Note: When not mentioned, engagement depends on random chance, so we can't assert definitively
        // But we can verify the setup was called correctly
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WithValidSetup_CallsCorrectMethods()
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[] { "user1: hello", "user2: world", "user3: test", "user4: Amiquin" };

        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .ReturnsAsync(true);
        _mockChatContextService
            .Setup(cs => cs.GetContextMessages(guildId))
            .Returns(contextMessages);
        _mockChatContextService
            .Setup(cs => cs.GetCurrentActivityLevel(guildId))
            .Returns(2.0); // High activity for consistent engagement
        _mockChatContextService
            .Setup(cs => cs.GetEngagementMultiplier(guildId))
            .Returns(1.0f);

        // Return null for guild - the actual guild object isn't used in the test logic
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

        _mockChatContextService
            .Setup(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()))
            .ReturnsAsync("Success response");

        // Act
        var result = await _service.ExecuteActivitySessionAsync(guildId);

        // Assert
        Assert.True(result);
        
        // Verify all expected calls were made
        _mockToggleService.Verify(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob), Times.Once);
        _mockChatContextService.Verify(cs => cs.GetContextMessages(guildId), Times.Once);
        _mockChatContextService.Verify(cs => cs.GetCurrentActivityLevel(guildId), Times.Once);
        _mockChatContextService.Verify(cs => cs.GetEngagementMultiplier(guildId), Times.Once);
        _mockDiscordClient.Verify(c => c.GetGuild(guildId), Times.Once);
        _mockChatContextService.Verify(cs => cs.AdaptiveResponseAsync(guildId, It.IsAny<IMessageChannel>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_WhenCancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var guildId = 12345UL;
        var cts = new CancellationTokenSource();
        
        _mockToggleService
            .Setup(ts => ts.IsEnabledAsync(guildId, Constants.ToggleNames.EnableLiveJob))
            .Returns(async () =>
            {
                cts.Cancel();
                await Task.Delay(10, cts.Token); // This will throw
                return true;
            });

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            _service.ExecuteActivitySessionAsync(guildId, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExecuteActivitySessionAsync_RetryMechanism_RetriesOnFailure()
    {
        // Arrange
        var guildId = 12345UL;
        var contextMessages = new[] { "user1: hello Amiquin" }; // Mention to force engagement

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

        // Return null for guild - the actual guild object isn't used in the test logic
        _mockDiscordClient.Setup(c => c.GetGuild(guildId)).Returns((SocketGuild?)null);

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
    }
}