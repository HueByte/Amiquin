using System.Diagnostics;

namespace Amiquin.Core.Services.ExternalProcessRunner;

/// <summary>
/// Service interface for creating and managing external processes.
/// Provides methods for creating FFmpeg and Piper processes for audio processing.
/// </summary>
public interface IExternalProcessRunnerService
{
    /// <summary>
    /// Creates an FFmpeg process for audio conversion and streaming.
    /// </summary>
    /// <param name="audioPath">The path to the input audio file.</param>
    /// <returns>A configured Process instance for FFmpeg.</returns>
    Process CreateFfmpegProcess(string audioPath);

    /// <summary>
    /// Creates a Piper process for text-to-speech conversion.
    /// </summary>
    /// <param name="piperCommand">The Piper command to execute.</param>
    /// <param name="modelPath">The path to the TTS model file.</param>
    /// <param name="ttsOutputPath">The output path for the generated audio file.</param>
    /// <returns>A configured Process instance for Piper.</returns>
    Process CreatePiperProcess(string piperCommand, string modelPath, string ttsOutputPath);
}