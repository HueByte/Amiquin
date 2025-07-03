using Discord;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.Voice;

public class MockVoiceServiceTests
{
    private readonly MockVoiceService _mockVoiceService;

    public MockVoiceServiceTests()
    {
        _mockVoiceService = new MockVoiceService();
    }

    [Fact]
    public async Task CreateTextToSpeechAudioAsync_ShouldReturnFakePathAndIncrementCounter()
    {
        // Arrange
        var text = "Hello, this is a test";

        // Act
        var result = await _mockVoiceService.CreateTextToSpeechAudioAsync(text);

        // Assert
        Assert.StartsWith("fake/path/to/audio_", result);
        Assert.EndsWith(".wav", result);
        Assert.Equal(1, _mockVoiceService.CreateTextToSpeechAudioAsyncCallCount);
        Assert.Equal(text, _mockVoiceService.LastText);
    }

    [Fact]
    public async Task JoinAsync_ShouldTrackChannelAndIncrementCounter()
    {
        // Arrange
        var guildId = 123456789UL;
        var voiceChannelMock = new Mock<IVoiceChannel>();
        voiceChannelMock.Setup(v => v.GuildId).Returns(guildId);

        // Act
        await _mockVoiceService.JoinAsync(voiceChannelMock.Object);

        // Assert
        Assert.Equal(1, _mockVoiceService.JoinAsyncCallCount);
        Assert.Equal(voiceChannelMock.Object, _mockVoiceService.LastVoiceChannel);
        Assert.True(_mockVoiceService.IsConnectedTo(guildId));
    }

    [Fact]
    public async Task LeaveAsync_ShouldRemoveChannelAndIncrementCounter()
    {
        // Arrange
        var guildId = 123456789UL;
        var voiceChannelMock = new Mock<IVoiceChannel>();
        voiceChannelMock.Setup(v => v.GuildId).Returns(guildId);

        // First join the channel
        await _mockVoiceService.JoinAsync(voiceChannelMock.Object);
        Assert.True(_mockVoiceService.IsConnectedTo(guildId));

        // Act
        await _mockVoiceService.LeaveAsync(voiceChannelMock.Object);

        // Assert
        Assert.Equal(1, _mockVoiceService.LeaveAsyncCallCount);
        Assert.Equal(voiceChannelMock.Object, _mockVoiceService.LastVoiceChannel);
        Assert.False(_mockVoiceService.IsConnectedTo(guildId));
    }

    [Fact]
    public async Task SpeakAsync_ShouldCallCreateAndStreamAudioMethods()
    {
        // Arrange
        var text = "Hello, this is a test";
        var voiceChannelMock = new Mock<IVoiceChannel>();

        // Act
        await _mockVoiceService.SpeakAsync(voiceChannelMock.Object, text);

        // Assert
        Assert.Equal(1, _mockVoiceService.SpeakAsyncCallCount);
        Assert.Equal(1, _mockVoiceService.CreateTextToSpeechAudioAsyncCallCount);
        Assert.Equal(1, _mockVoiceService.StreamAudioAsyncCallCount);
        Assert.Equal(voiceChannelMock.Object, _mockVoiceService.LastVoiceChannel);
        Assert.Equal(text, _mockVoiceService.LastText);
        Assert.NotNull(_mockVoiceService.LastFilePath);
        Assert.StartsWith("fake/path/to/audio_", _mockVoiceService.LastFilePath);
    }

    [Fact]
    public async Task StreamAudioAsync_ShouldTrackParametersAndIncrementCounter()
    {
        // Arrange
        var filePath = "path/to/audio.mp3";
        var voiceChannelMock = new Mock<IVoiceChannel>();

        // Act
        await _mockVoiceService.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        Assert.Equal(1, _mockVoiceService.StreamAudioAsyncCallCount);
        Assert.Equal(voiceChannelMock.Object, _mockVoiceService.LastVoiceChannel);
        Assert.Equal(filePath, _mockVoiceService.LastFilePath);
    }

    [Fact]
    public async Task Reset_ShouldClearAllCountersAndTrackedValues()
    {
        // Arrange
        var text = "Hello, this is a test";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456789UL);

        // Set up tracked values
        await _mockVoiceService.JoinAsync(voiceChannelMock.Object);
        await _mockVoiceService.SpeakAsync(voiceChannelMock.Object, text);

        // Verify values are tracked
        Assert.Equal(1, _mockVoiceService.JoinAsyncCallCount);
        Assert.Equal(1, _mockVoiceService.SpeakAsyncCallCount);
        Assert.Equal(1, _mockVoiceService.CreateTextToSpeechAudioAsyncCallCount);
        Assert.Equal(1, _mockVoiceService.StreamAudioAsyncCallCount);
        Assert.NotNull(_mockVoiceService.LastVoiceChannel);
        Assert.NotNull(_mockVoiceService.LastText);
        Assert.NotNull(_mockVoiceService.LastFilePath);
        Assert.True(_mockVoiceService.IsConnectedTo(123456789UL));

        // Act
        _mockVoiceService.Reset();

        // Assert
        Assert.Equal(0, _mockVoiceService.JoinAsyncCallCount);
        Assert.Equal(0, _mockVoiceService.SpeakAsyncCallCount);
        Assert.Equal(0, _mockVoiceService.CreateTextToSpeechAudioAsyncCallCount);
        Assert.Equal(0, _mockVoiceService.StreamAudioAsyncCallCount);
        Assert.Equal(0, _mockVoiceService.LeaveAsyncCallCount);
        Assert.Null(_mockVoiceService.LastVoiceChannel);
        Assert.Null(_mockVoiceService.LastText);
        Assert.Null(_mockVoiceService.LastFilePath);
        Assert.False(_mockVoiceService.IsConnectedTo(123456789UL));
    }

    [Fact]
    public async Task IsConnectedTo_ShouldReturnCorrectConnectionState()
    {
        // Arrange
        var guildId1 = 123456789UL;
        var guildId2 = 987654321UL;
        var voiceChannelMock1 = new Mock<IVoiceChannel>();
        voiceChannelMock1.Setup(v => v.GuildId).Returns(guildId1);
        var voiceChannelMock2 = new Mock<IVoiceChannel>();
        voiceChannelMock2.Setup(v => v.GuildId).Returns(guildId2);

        // Act
        await _mockVoiceService.JoinAsync(voiceChannelMock1.Object);

        // Assert
        Assert.True(_mockVoiceService.IsConnectedTo(guildId1));
        Assert.False(_mockVoiceService.IsConnectedTo(guildId2));
    }
}