using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Amiquin.Core.Services.ExternalProcessRunner;

/// <summary>
/// Enhanced implementation of external process management for audio processing.
/// Provides robust process creation, monitoring, and cleanup with cross-platform support.
/// </summary>
public class ExternalProcessRunnerService : IExternalProcessRunnerService, IDisposable
{
    private readonly ILogger<ExternalProcessRunnerService> _logger;
    private readonly ConcurrentDictionary<int, ManagedProcess> _managedProcesses = new();
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalProcessRunnerService"/> class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    public ExternalProcessRunnerService(ILogger<ExternalProcessRunnerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Process CreatePiperProcess(string piperCommand, string modelPath, string ttsOutputPath)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);

        ValidateProcessParameters(piperCommand, nameof(piperCommand));
        ValidateProcessParameters(modelPath, nameof(modelPath));
        ValidateProcessParameters(ttsOutputPath, nameof(ttsOutputPath));

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"TTS model file not found: {modelPath}", modelPath);
        }

        // Ensure output directory exists
        var outputDirectory = Path.GetDirectoryName(ttsOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            _logger.LogDebug("Created TTS output directory: {OutputDirectory}", outputDirectory);
        }

        var args = $"--model \"{EscapeProcessArgument(modelPath)}\" --output_file \"{EscapeProcessArgument(ttsOutputPath)}\"";
        _logger.LogInformation("Creating Piper process - Command: [{PiperCommand}] Args: [{Args}]", piperCommand, args);

        var startInfo = new ProcessStartInfo
        {
            FileName = piperCommand,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        // Add environment variables for better process isolation
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.Environment["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "";
        }
        else
        {
            startInfo.Environment["LD_LIBRARY_PATH"] = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? "";
        }

        var process = CreateManagedProcess(startInfo, ProcessType.Piper);
        return process.Process;
    }

    /// <inheritdoc/>
    public Process CreateFfmpegProcess(string audioPath)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().Name);

        ValidateProcessParameters(audioPath, nameof(audioPath));

        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException($"Audio file not found: {audioPath}", audioPath);
        }

        // FFmpeg arguments optimized for Discord audio streaming
        var args = $"-re -hide_banner -loglevel error -i \"{EscapeProcessArgument(audioPath)}\" " +
                   $"-ac 2 -f s16le -ar 48000 -acodec pcm_s16le pipe:1";

        _logger.LogInformation("Creating FFmpeg process for audio: {AudioPath}", Path.GetFileName(audioPath));
        _logger.LogDebug("FFmpeg arguments: {Args}", args);

        var startInfo = new ProcessStartInfo
        {
            FileName = GetFfmpegExecutableName(),
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };

        var process = CreateManagedProcess(startInfo, ProcessType.FFmpeg);
        return process.Process;
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateExecutableAsync(string executableName, CancellationToken cancellationToken = default)
    {
        try
        {
            var versionArgs = executableName.ToLowerInvariant() switch
            {
                "ffmpeg" => "-version",
                "piper" => "--help",
                _ => "--version"
            };

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executableName,
                    Arguments = versionArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            var isValid = process.ExitCode == 0;
            _logger.LogDebug("Executable validation for {Executable}: {IsValid}", executableName, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate executable: {Executable}", executableName);
            return false;
        }
    }

    /// <inheritdoc/>
    public void CleanupProcess(int processId)
    {
        if (_managedProcesses.TryRemove(processId, out var managedProcess))
        {
            managedProcess.Dispose();
            _logger.LogDebug("Cleaned up managed process {ProcessId}", processId);
        }
    }

    /// <inheritdoc/>
    public ProcessInfo[] GetManagedProcesses()
    {
        return _managedProcesses.Values
            .Where(mp => !mp.Process.HasExited)
            .Select(mp => new ProcessInfo
            {
                Id = mp.Process.Id,
                Type = mp.Type,
                StartTime = mp.StartTime,
                IsRunning = !mp.Process.HasExited
            })
            .ToArray();
    }

    /// <summary>
    /// Creates a managed process with monitoring and cleanup capabilities.
    /// </summary>
    private ManagedProcess CreateManagedProcess(ProcessStartInfo startInfo, ProcessType type)
    {
        var process = new Process { StartInfo = startInfo };

        // Set up process exit event handler
        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) =>
        {
            if (sender is Process p)
            {
                CleanupProcess(p.Id);
            }
        };

        var managedProcess = new ManagedProcess(process, type);

        try
        {
            process.Start();
            _managedProcesses.TryAdd(process.Id, managedProcess);

            _logger.LogDebug("Created {ProcessType} process with ID {ProcessId}", type, process.Id);
        }
        catch (Exception ex)
        {
            managedProcess.Dispose();
            _logger.LogError(ex, "Failed to start {ProcessType} process", type);
            throw;
        }

        return managedProcess;
    }

    /// <summary>
    /// Gets the appropriate FFmpeg executable name based on the platform.
    /// </summary>
    private static string GetFfmpegExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
    }

    /// <summary>
    /// Escapes process arguments to prevent command injection.
    /// </summary>
    private static string EscapeProcessArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return argument;

        // Basic escaping for common dangerous characters
        return argument.Replace("\"", "\\\"").Replace("&", "^&").Replace("|", "^|");
    }

    /// <summary>
    /// Validates process parameters to prevent null or empty values.
    /// </summary>
    private static void ValidateProcessParameters(string parameter, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter, parameterName);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _logger.LogInformation("Disposing ExternalProcessRunnerService with {ProcessCount} managed processes",
            _managedProcesses.Count);

        // Clean up all managed processes
        var processes = _managedProcesses.Values.ToList();
        foreach (var managedProcess in processes)
        {
            try
            {
                managedProcess.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing managed process {ProcessId}",
                    managedProcess.Process?.Id ?? -1);
            }
        }

        _managedProcesses.Clear();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents a managed process with metadata and cleanup capabilities.
    /// </summary>
    private sealed class ManagedProcess : IDisposable
    {
        public Process Process { get; }
        public ProcessType Type { get; }
        public DateTime StartTime { get; }

        public ManagedProcess(Process process, ProcessType type)
        {
            Process = process;
            Type = type;
            StartTime = DateTime.UtcNow;
        }

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill();
                }
            }
            catch
            {
                // Ignore disposal errors
            }
            finally
            {
                Process?.Dispose();
            }
        }
    }

    /// <summary>
    /// Represents the type of external process.
    /// </summary>
    public enum ProcessType
    {
        FFmpeg,
        Piper
    }

    /// <summary>
    /// Information about a managed process.
    /// </summary>
    public class ProcessInfo
    {
        public int Id { get; set; }
        public ProcessType Type { get; set; }
        public DateTime StartTime { get; set; }
        public bool IsRunning { get; set; }
    }
}