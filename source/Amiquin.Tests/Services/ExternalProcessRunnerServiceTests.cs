using Amiquin.Core.Services.ExternalProcessRunner;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.ExternalProcessRunner;

public class ExternalProcessRunnerServiceTests
{
    private readonly Mock<ILogger<ExternalProcessRunnerService>> _loggerMock;
    private readonly ExternalProcessRunnerService _sut; // System Under Test

    public ExternalProcessRunnerServiceTests()
    {
        _loggerMock = new Mock<ILogger<ExternalProcessRunnerService>>();
        _sut = new ExternalProcessRunnerService(_loggerMock.Object);
    }

    [Fact]
    public void CreatePiperProcess_ShouldReturnProcessWithCorrectConfiguration()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = @"C:\models\voice.onnx";
        var ttsOutputPath = @"C:\output\audio.wav";

        // Act
        var process = _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

        // Assert
        Assert.NotNull(process);
        Assert.Equal(piperCommand, process.StartInfo.FileName);
        Assert.Contains($"--model \"{modelPath}\"", process.StartInfo.Arguments);
        Assert.Contains($"--output_file \"{ttsOutputPath}\"", process.StartInfo.Arguments);
        Assert.True(process.StartInfo.RedirectStandardInput);
        Assert.True(process.StartInfo.RedirectStandardOutput);
        Assert.True(process.StartInfo.RedirectStandardError);
        Assert.False(process.StartInfo.UseShellExecute);
        Assert.True(process.StartInfo.CreateNoWindow);
    }

    [Fact]
    public void CreatePiperProcess_ShouldLogCorrectInformation()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = @"C:\models\voice.onnx";
        var ttsOutputPath = @"C:\output\audio.wav";
        var expectedArgs = $"--model \"{modelPath}\" --output_file \"{ttsOutputPath}\"";

        // Act
        _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Piper command: [{piperCommand}] Args: [{expectedArgs}]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateFfmpegProcess_ShouldReturnProcessWithCorrectConfiguration()
    {
        // Arrange
        var audioPath = @"C:\audio\input.mp3";

        // Act
        var process = _sut.CreateFfmpegProcess(audioPath);

        // Assert
        Assert.NotNull(process);
        Assert.Equal("ffmpeg", process.StartInfo.FileName);
        Assert.Contains($"-i \"{audioPath}\"", process.StartInfo.Arguments);
        Assert.Contains("-re -hide_banner -loglevel panic", process.StartInfo.Arguments);
        Assert.Contains("-ac 2 -f s16le -ar 48000 pipe:1", process.StartInfo.Arguments);
        Assert.False(process.StartInfo.UseShellExecute);
        Assert.True(process.StartInfo.RedirectStandardOutput);
    }

    [Fact]
    public void CreateFfmpegProcess_ShouldLogCorrectInformation()
    {
        // Arrange
        var audioPath = @"C:\audio\input.mp3";

        // Act
        _sut.CreateFfmpegProcess(audioPath);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Creating ffmpeg process for audio path {audioPath}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreatePiperProcess_WithSpecialCharactersInPaths_ShouldHandleCorrectly()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = @"C:\models with spaces\voice model.onnx";
        var ttsOutputPath = @"C:\output with spaces\audio file.wav";

        // Act
        var process = _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

        // Assert
        Assert.NotNull(process);
        Assert.Contains($"--model \"{modelPath}\"", process.StartInfo.Arguments);
        Assert.Contains($"--output_file \"{ttsOutputPath}\"", process.StartInfo.Arguments);
    }

    [Fact]
    public void CreateFfmpegProcess_WithSpecialCharactersInPath_ShouldHandleCorrectly()
    {
        // Arrange
        var audioPath = @"C:\audio with spaces\input file.mp3";

        // Act
        var process = _sut.CreateFfmpegProcess(audioPath);

        // Assert
        Assert.NotNull(process);
        Assert.Contains($"-i \"{audioPath}\"", process.StartInfo.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void CreatePiperProcess_WithEmptyOrNullPaths_ShouldStillCreateProcess(string? nullOrEmptyValue)
    {
        // Arrange
        var piperCommand = "piper";

        // Act
        var process = _sut.CreatePiperProcess(piperCommand, nullOrEmptyValue ?? "", nullOrEmptyValue ?? "");

        // Assert
        Assert.NotNull(process);
        Assert.Equal(piperCommand, process.StartInfo.FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void CreateFfmpegProcess_WithEmptyOrNullPath_ShouldStillCreateProcess(string? nullOrEmptyValue)
    {
        // Act
        var process = _sut.CreateFfmpegProcess(nullOrEmptyValue ?? "");

        // Assert
        Assert.NotNull(process);
        Assert.Equal("ffmpeg", process.StartInfo.FileName);
    }
}