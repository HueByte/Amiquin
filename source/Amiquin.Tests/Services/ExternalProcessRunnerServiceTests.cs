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

    [Fact(Skip = "Process starting test - requires piper executable")]
    public void CreatePiperProcess_ShouldReturnProcessWithCorrectConfiguration()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = Path.Combine(Path.GetTempPath(), "voice.onnx");
        var ttsOutputPath = Path.Combine(Path.GetTempPath(), "audio.wav");
        
        // Create temporary model file for test
        File.WriteAllText(modelPath, "dummy");

        try
        {
            // Act
            var process = _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

            // Assert
            Assert.NotNull(process);
            Assert.Equal(piperCommand, process.StartInfo.FileName);
            Assert.Contains("--model", process.StartInfo.Arguments);
            Assert.Contains("--output_file", process.StartInfo.Arguments);
            Assert.True(process.StartInfo.RedirectStandardInput);
            Assert.True(process.StartInfo.RedirectStandardOutput);
            Assert.True(process.StartInfo.RedirectStandardError);
            Assert.False(process.StartInfo.UseShellExecute);
            Assert.True(process.StartInfo.CreateNoWindow);
        }
        finally
        {
            // Cleanup
            if (File.Exists(modelPath)) File.Delete(modelPath);
        }
    }

    [Fact(Skip = "Process starting test - requires piper executable")]
    public void CreatePiperProcess_ShouldLogCorrectInformation()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = Path.Combine(Path.GetTempPath(), "voice.onnx");
        var ttsOutputPath = Path.Combine(Path.GetTempPath(), "audio.wav");
        
        // Create temporary model file for test
        File.WriteAllText(modelPath, "dummy");

        try
        {
            // Act
            _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating Piper process")),
                    It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(modelPath)) File.Delete(modelPath);
        }
    }

    [Fact(Skip = "Process starting test - requires ffmpeg executable")]
    public void CreateFfmpegProcess_ShouldReturnProcessWithCorrectConfiguration()
    {
        // Arrange
        var audioPath = Path.Combine(Path.GetTempPath(), "input.mp3");
        
        // Create temporary audio file for test
        File.WriteAllText(audioPath, "dummy");

        try
        {
            // Act
            var process = _sut.CreateFfmpegProcess(audioPath);

            // Assert
            Assert.NotNull(process);
            Assert.True(process.StartInfo.FileName.Contains("ffmpeg"));
            Assert.Contains("-i", process.StartInfo.Arguments);
            Assert.Contains("-re -hide_banner -loglevel error", process.StartInfo.Arguments);
            Assert.Contains("-ac 2 -f s16le -ar 48000", process.StartInfo.Arguments);
            Assert.False(process.StartInfo.UseShellExecute);
            Assert.True(process.StartInfo.RedirectStandardOutput);
        }
        finally
        {
            // Cleanup
            if (File.Exists(audioPath)) File.Delete(audioPath);
        }
    }

    [Fact(Skip = "Process starting test - requires ffmpeg executable")]
    public void CreateFfmpegProcess_ShouldLogCorrectInformation()
    {
        // Arrange
        var audioPath = Path.Combine(Path.GetTempPath(), "input.mp3");
        
        // Create temporary audio file for test
        File.WriteAllText(audioPath, "dummy");

        try
        {
            // Act
            _sut.CreateFfmpegProcess(audioPath);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating FFmpeg process for audio")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
        finally
        {
            // Cleanup
            if (File.Exists(audioPath)) File.Delete(audioPath);
        }
    }

    [Fact(Skip = "Process starting test - requires piper executable")]
    public void CreatePiperProcess_WithSpecialCharactersInPaths_ShouldHandleCorrectly()
    {
        // Arrange
        var piperCommand = "piper";
        var modelPath = Path.Combine(Path.GetTempPath(), "models with spaces", "voice model.onnx");
        var ttsOutputPath = Path.Combine(Path.GetTempPath(), "output with spaces", "audio file.wav");
        
        // Create directories and temporary model file for test
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        File.WriteAllText(modelPath, "dummy");

        try
        {
            // Act
            var process = _sut.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

            // Assert
            Assert.NotNull(process);
            Assert.Contains("--model", process.StartInfo.Arguments);
            Assert.Contains("--output_file", process.StartInfo.Arguments);
        }
        finally
        {
            // Cleanup
            if (File.Exists(modelPath)) File.Delete(modelPath);
            var modelDir = Path.GetDirectoryName(modelPath);
            if (Directory.Exists(modelDir)) Directory.Delete(modelDir, true);
        }
    }

    [Fact(Skip = "Process starting test - requires ffmpeg executable")]
    public void CreateFfmpegProcess_WithSpecialCharactersInPath_ShouldHandleCorrectly()
    {
        // Arrange
        var audioPath = Path.Combine(Path.GetTempPath(), "audio with spaces", "input file.mp3");
        
        // Create directory and temporary audio file for test
        Directory.CreateDirectory(Path.GetDirectoryName(audioPath)!);
        File.WriteAllText(audioPath, "dummy");

        try
        {
            // Act
            var process = _sut.CreateFfmpegProcess(audioPath);

            // Assert
            Assert.NotNull(process);
            Assert.Contains("-i", process.StartInfo.Arguments);
        }
        finally
        {
            // Cleanup
            if (File.Exists(audioPath)) File.Delete(audioPath);
            var audioDir = Path.GetDirectoryName(audioPath);
            if (Directory.Exists(audioDir)) Directory.Delete(audioDir, true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void CreatePiperProcess_WithEmptyOrNullPaths_ShouldThrowException(string? nullOrEmptyValue)
    {
        // Arrange
        var piperCommand = "piper";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _sut.CreatePiperProcess(piperCommand, nullOrEmptyValue ?? "", nullOrEmptyValue ?? ""));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void CreateFfmpegProcess_WithEmptyOrNullPath_ShouldThrowException(string? nullOrEmptyValue)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _sut.CreateFfmpegProcess(nullOrEmptyValue ?? ""));
    }
}