using System.Diagnostics;
using static Amiquin.Core.Services.ExternalProcessRunner.ExternalProcessRunnerService;

namespace Amiquin.Core.Services.ExternalProcessRunner;

/// <summary>
/// Service interface for creating and managing external processes.
/// Provides methods for creating FFmpeg and Piper processes for audio processing with enhanced management capabilities.
/// </summary>
public interface IExternalProcessRunnerService
{
    /// <summary>
    /// Creates an FFmpeg process for audio conversion and streaming.
    /// </summary>
    /// <param name="audioPath">The path to the input audio file.</param>
    /// <returns>A configured Process instance for FFmpeg.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the audio file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when audioPath is null or whitespace.</exception>
    Process CreateFfmpegProcess(string audioPath);

    /// <summary>
    /// Creates a Piper process for text-to-speech conversion.
    /// </summary>
    /// <param name="piperCommand">The Piper command to execute.</param>
    /// <param name="modelPath">The path to the TTS model file.</param>
    /// <param name="ttsOutputPath">The output path for the generated audio file.</param>
    /// <returns>A configured Process instance for Piper.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
    /// <exception cref="ArgumentException">Thrown when any parameter is null or whitespace.</exception>
    Process CreatePiperProcess(string piperCommand, string modelPath, string ttsOutputPath);

    /// <summary>
    /// Validates that an executable is available and functional.
    /// </summary>
    /// <param name="executableName">The name of the executable to validate.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>True if the executable is available and responds correctly; otherwise, false.</returns>
    Task<bool> ValidateExecutableAsync(string executableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up a managed process by its process ID.
    /// </summary>
    /// <param name="processId">The ID of the process to clean up.</param>
    void CleanupProcess(int processId);

    /// <summary>
    /// Gets information about all currently managed processes.
    /// </summary>
    /// <returns>An array of ProcessInfo containing details about managed processes.</returns>
    ProcessInfo[] GetManagedProcesses();
}