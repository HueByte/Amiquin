using System.Diagnostics;

namespace Amiquin.Core.Services.ExternalProcessRunner;

public interface IExternalProcessRunnerService
{
    Process CreateFfmpegProcess(string audioPath);
    Process CreatePiperProcess(string piperCommand, string modelPath, string ttsOutputPath);
}
