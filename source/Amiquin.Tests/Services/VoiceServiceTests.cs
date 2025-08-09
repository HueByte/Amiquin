using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ExternalProcessRunner;
using Amiquin.Core.Services.Voice;
using Amiquin.Core.Services.Voice.Models;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;
using Xunit;

namespace Amiquin.Tests.Services.Voice;

public class VoiceServiceTests
{
    private readonly Mock<ILogger<VoiceService>> _loggerMock;
    private readonly Mock<IVoiceStateManager> _voiceStateManagerMock;
    private readonly Mock<IChatSemaphoreManager> _chatSemaphoreManagerMock;
    private readonly Mock<IOptions<ExternalOptions>> _externalOptionsMock;
    private readonly Mock<IOptions<VoiceOptions>> _voiceOptionsMock;
    private readonly Mock<IExternalProcessRunnerService> _externalProcessRunnerMock;
    private readonly VoiceService _sut; // System Under Test

    public VoiceServiceTests()
    {
        _loggerMock = new Mock<ILogger<VoiceService>>();
        _voiceStateManagerMock = new Mock<IVoiceStateManager>();
        _chatSemaphoreManagerMock = new Mock<IChatSemaphoreManager>();

        var externalOptions = new ExternalOptions
        {
            NewsApiUrl = "https://inshorts.com/"
        };
        _externalOptionsMock = new Mock<IOptions<ExternalOptions>>();
        _externalOptionsMock.Setup(x => x.Value).Returns(externalOptions);

        var voiceOptions = new VoiceOptions
        {
            TTSModelName = "en_GB-northern_english_male-medium",
            PiperCommand = "piper",
            Enabled = true
        };
        _voiceOptionsMock = new Mock<IOptions<VoiceOptions>>();
        _voiceOptionsMock.Setup(x => x.Value).Returns(voiceOptions);

        _externalProcessRunnerMock = new Mock<IExternalProcessRunnerService>();

        _sut = new VoiceService(
            _loggerMock.Object,
            _voiceStateManagerMock.Object,
            _chatSemaphoreManagerMock.Object,
            _externalOptionsMock.Object,
            _voiceOptionsMock.Object,
            _externalProcessRunnerMock.Object
        );
    }

    [Fact]
    public async Task JoinAsync_ShouldCallConnectVoiceChannelAsync()
    {
        // Arrange
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);

        // Act
        await _sut.JoinAsync(voiceChannelMock.Object);

        // Assert
        _voiceStateManagerMock.Verify(v => v.ConnectVoiceChannelAsync(voiceChannelMock.Object), Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_ShouldCallDisconnectVoiceChannelAsync()
    {
        // Arrange
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);

        // Act
        await _sut.LeaveAsync(voiceChannelMock.Object);

        // Assert
        _voiceStateManagerMock.Verify(v => v.DisconnectVoiceChannelAsync(voiceChannelMock.Object), Times.Once);
    }

    [Fact(Skip = "Complex external process mocking - requires integration test approach")]
    public async Task CreateTextToSpeechAudioAsync_ShouldCreateAudioFile()
    {
        // Arrange
        var text = "Hello, this is a test";
        var expectedOutputPath = $"path/to/output.wav"; // This will be dynamic in the real implementation

        var processMock = new Mock<Process>();
        var standardInputMock = new Mock<StreamWriter>();

        processMock.Setup(p => p.StandardInput).Returns(standardInputMock.Object);
        processMock.Setup(p => p.Start()).Returns(true);

        _externalProcessRunnerMock.Setup(x => x.CreatePiperProcess(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(processMock.Object);

        // Voice options are already configured in the constructor through the mock

        // Act
        var result = await _sut.CreateTextToSpeechAudioAsync(text);

        // Assert
        Assert.NotNull(result);
        Assert.EndsWith(".wav", result);
        processMock.Verify(p => p.Start(), Times.Once);
        standardInputMock.Verify(si => si.WriteLineAsync(text), Times.Once);
    }

    [Fact(Skip = "Complex Discord mocking - requires integration test approach")]
    public async Task SpeakAsync_ShouldCreateAudioAndStreamIt()
    {
        // Arrange
        var text = "Hello, this is a test";
        var audioFilePath = "path/to/generated/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        var semaphoreMock = new Mock<SemaphoreSlim>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        guildMock.Setup(g => g.Id).Returns(123456UL);
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        _chatSemaphoreManagerMock.Setup(c => c.GetOrCreateVoiceSemaphore(123456UL))
            .Returns(semaphoreMock.Object);

        // Set up the mock to return our audio file path when CreateTextToSpeechAudioAsync is called
        // This is a partial mock of our System Under Test
        var voiceServiceMock = new Mock<VoiceService>(
            _loggerMock.Object,
            _voiceStateManagerMock.Object,
            _chatSemaphoreManagerMock.Object,
            _externalOptionsMock.Object,
            _voiceOptionsMock.Object,
            _externalProcessRunnerMock.Object
        )
        { CallBase = true };

        voiceServiceMock.Setup(v => v.CreateTextToSpeechAudioAsync(text))
            .ReturnsAsync(audioFilePath);

        voiceServiceMock.Setup(v => v.StreamAudioAsync(voiceChannelMock.Object, audioFilePath))
            .Returns(Task.CompletedTask);

        // Act
        await voiceServiceMock.Object.SpeakAsync(voiceChannelMock.Object, text);

        // Assert
        voiceServiceMock.Verify(v => v.CreateTextToSpeechAudioAsync(text), Times.Once);
        voiceServiceMock.Verify(v => v.StreamAudioAsync(voiceChannelMock.Object, audioFilePath), Times.Once);
        semaphoreMock.Verify(s => s.WaitAsync(), Times.Once);
        semaphoreMock.Verify(s => s.Release(), Times.Once);
    }

    [Fact(Skip = "Complex Discord AudioClient mocking - requires integration test approach")]
    public async Task StreamAudioAsync_WithValidAudioClient_ShouldStreamAudio()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        var audioClientMock = new Mock<IAudioClient>();
        var audioOutStreamMock = new Mock<AudioOutStream>();
        var ffmpegProcessMock = new Mock<Process>();
        var ffmpegOutputStreamMock = new Mock<Stream>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        guildMock.Setup(g => g.Id).Returns(123456UL);
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        // Setup audio client - using CallBase to use default behavior
        audioClientMock.CallBase = true;

        var amiquinVoice = new AmiquinVoice
        {
            VoiceChannel = voiceChannelMock.Object,
            AudioClient = audioClientMock.Object,
            AudioOutStream = null
        };

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns(amiquinVoice);

        // Setup the FFmpeg process mock
        ffmpegProcessMock.Setup(p => p.StandardOutput).Returns(new Mock<StreamReader>().Object);
        ffmpegProcessMock.Setup(p => p.StandardOutput.BaseStream).Returns(ffmpegOutputStreamMock.Object);
        ffmpegProcessMock.Setup(p => p.Start()).Returns(true);

        _externalProcessRunnerMock.Setup(e => e.CreateFfmpegProcess(filePath))
            .Returns(ffmpegProcessMock.Object);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Verify other calls but skip CreatePCMStream due to optional parameter issues
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);
        audioClientMock.Verify(a => a.SetSpeakingAsync(true), Times.Once);
        audioClientMock.Verify(a => a.SetSpeakingAsync(false), Times.Once);
        ffmpegProcessMock.Verify(p => p.Start(), Times.Once);
    }

    [Fact(Skip = "Complex Discord mocking - requires integration test approach")]
    public async Task StreamAudioAsync_WithNullAudioClient_ShouldLogErrorAndReturn()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        var amiquinVoice = new AmiquinVoice
        {
            VoiceChannel = voiceChannelMock.Object,
            AudioClient = null,
            AudioOutStream = null
        };

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns(amiquinVoice);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);

        // Verify log error was called - we need to verify the specific log message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to stream audio")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((o, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task StreamAudioAsync_WithNullAmiquinVoice_ShouldLogErrorAndReturn()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns((AmiquinVoice?)null);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);

        // Verify log error was called with correct message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to retrieve AmiquinVoice")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((o, t) => true)),
            Times.Once);
    }

    [Fact]
    public async Task StreamAudioAsync_FailedToCreateFfmpegProcess_ShouldLogErrorAndReturn()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        var audioClientMock = new Mock<IAudioClient>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        var amiquinVoice = new AmiquinVoice
        {
            VoiceChannel = voiceChannelMock.Object,
            AudioClient = audioClientMock.Object,
            AudioOutStream = null
        };

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns(amiquinVoice);

        _externalProcessRunnerMock.Setup(e => e.CreateFfmpegProcess(filePath))
            .Returns((Process?)null!);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);
        _externalProcessRunnerMock.Verify(e => e.CreateFfmpegProcess(filePath), Times.Once);

        // Verify log error was called with correct message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to create FFmpeg process")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((o, t) => true)),
            Times.Once);
    }

    [Fact(Skip = "Complex external process mocking - requires integration test approach")]
    public async Task StreamAudioAsync_FfmpegStartThrowsException_ShouldLogErrorAndReturn()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        var audioClientMock = new Mock<IAudioClient>();
        var ffmpegProcessMock = new Mock<Process>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        var amiquinVoice = new AmiquinVoice
        {
            VoiceChannel = voiceChannelMock.Object,
            AudioClient = audioClientMock.Object,
            AudioOutStream = null
        };

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns(amiquinVoice);

        ffmpegProcessMock.Setup(p => p.Start())
            .Throws(new InvalidOperationException("Failed to start process"));

        _externalProcessRunnerMock.Setup(e => e.CreateFfmpegProcess(filePath))
            .Returns(ffmpegProcessMock.Object);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);
        _externalProcessRunnerMock.Verify(e => e.CreateFfmpegProcess(filePath), Times.Once);
        ffmpegProcessMock.Verify(p => p.Start(), Times.Once);

        // Verify log error was called with correct message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error starting FFmpeg process")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((o, t) => true)),
            Times.Once);
    }

    [Fact(Skip = "Complex external process mocking - requires integration test approach")]
    public async Task StreamAudioAsync_NullFfmpegOutputStream_ShouldLogErrorAndReturn()
    {
        // Arrange
        var filePath = "path/to/audio.wav";
        var voiceChannelMock = new Mock<IVoiceChannel>();
        var guildMock = new Mock<IGuild>();
        var audioClientMock = new Mock<IAudioClient>();
        var ffmpegProcessMock = new Mock<Process>();
        var streamReaderMock = new Mock<StreamReader>();

        guildMock.Setup(g => g.Name).Returns("TestGuild");
        voiceChannelMock.Setup(v => v.Name).Returns("TestChannel");
        voiceChannelMock.Setup(v => v.Guild).Returns(guildMock.Object);
        voiceChannelMock.Setup(v => v.GuildId).Returns(123456UL);

        var amiquinVoice = new AmiquinVoice
        {
            VoiceChannel = voiceChannelMock.Object,
            AudioClient = audioClientMock.Object,
            AudioOutStream = null
        };

        _voiceStateManagerMock.Setup(v => v.GetAmiquinVoice(123456UL))
            .Returns(amiquinVoice);

        ffmpegProcessMock.Setup(p => p.Start())
            .Returns(true);

        ffmpegProcessMock.Setup(p => p.StandardOutput)
            .Returns(streamReaderMock.Object);

        streamReaderMock.Setup(s => s.BaseStream)
            .Returns((Stream?)null!);

        _externalProcessRunnerMock.Setup(e => e.CreateFfmpegProcess(filePath))
            .Returns(ffmpegProcessMock.Object);

        // Act
        await _sut.StreamAudioAsync(voiceChannelMock.Object, filePath);

        // Assert
        _voiceStateManagerMock.Verify(v => v.GetAmiquinVoice(123456UL), Times.Once);
        _externalProcessRunnerMock.Verify(e => e.CreateFfmpegProcess(filePath), Times.Once);
        ffmpegProcessMock.Verify(p => p.Start(), Times.Once);

        // Verify log error was called with correct message
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("FFmpeg output stream is null")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((o, t) => true)),
            Times.Once);
    }
}