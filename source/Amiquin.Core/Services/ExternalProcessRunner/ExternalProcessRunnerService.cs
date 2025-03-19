using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ExternalProcessRunner;

public class ExternalProcessRunnerService : IExternalProcessRunnerService
{
    private readonly ILogger _logger;

    public ExternalProcessRunnerService(ILogger<ExternalProcessRunnerService> logger)
    {
        _logger = logger;
    }

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