using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Amiquin.Core.Services.ExternalProcessRunner;

/// <summary>
/// Implementation of the <see cref="IExternalProcessRunnerService"/> interface.
/// Manages creation and execution of external processes for audio processing.
/// </summary>
public class ExternalProcessRunnerService : IExternalProcessRunnerService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalProcessRunnerService"/> class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    public ExternalProcessRunnerService(ILogger<ExternalProcessRunnerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Process CreatePiperProcess(string piperCommand, string modelPath, string ttsOutputPath)
    {
        string args = $"--model \"{modelPath}\" --output_file \"{ttsOutputPath}\"";
        _logger.LogInformation("Piper command: [{PiperCommand}] Args: [{args}] ", piperCommand, args);

        // Prepare the process start info to execute the TTS command directly.
        var startInfo = new ProcessStartInfo
        {
            FileName = piperCommand,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return new Process { StartInfo = startInfo };
    }

    /// <inheritdoc/>
    public Process CreateFfmpegProcess(string audioPath)
    {
        _logger.LogInformation("Creating ffmpeg process for audio path {AudioPath}", audioPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-re -hide_banner -loglevel panic -i \"{audioPath}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };

        return new Process { StartInfo = startInfo };
    }
}