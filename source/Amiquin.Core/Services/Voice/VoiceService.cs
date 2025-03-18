using System.Diagnostics;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Voice;

public class VoiceService : IVoiceService
{
    private readonly ILogger<VoiceService> _logger;
    private readonly IVoiceStateManager _voiceStateManager;
    private readonly IConfiguration _configuration;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly ExternalOptions _externalOptions;

    public VoiceService(ILogger<VoiceService> logger, IVoiceStateManager voiceStateManager, IConfiguration configuration, IChatSemaphoreManager chatSemaphoreManager, IOptions<ExternalOptions> externalOptions)
    {
        _logger = logger;
        _voiceStateManager = voiceStateManager;
        _configuration = configuration;
        _chatSemaphoreManager = chatSemaphoreManager;
        _externalOptions = externalOptions.Value;
    }

    public async Task SpeakAsync(IVoiceChannel voiceChannel, string text)
    {
        var semaphore = _chatSemaphoreManager.GetOrCreateVoiceSemaphore(voiceChannel.GuildId);
        await semaphore.WaitAsync();
        try
        {
            var filePath = await CreateTextToSpeechAudioAsync(text);
            await StreamAudioAsync(voiceChannel, filePath);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<string> CreateTextToSpeechAudioAsync(string text)
    {
        _logger.LogInformation("Creating text to speech audio for text: {Text}", text);

        var guid = Guid.NewGuid().ToString();
        string? modelName = _configuration.GetValue<string>(Constants.TTSModelName) ?? _externalOptions.ModelName;
        string? piperCommand = _configuration.GetValue<string>(Constants.PiperCommand) ?? _externalOptions.PiperCommand;

        var modelPath = Path.Join(Constants.TTSBasePath,
            string.IsNullOrEmpty(modelName) ? "en_GB-northern_english_male-medium.onnx" : $"{modelName}.onnx");
        var ttsOutputPath = Path.Join(Constants.TTSBasePath, "output", $"o_{guid}.wav");

        CreateRequiredDirectories();

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

        _logger.LogDebug("Starting process: {FileName} {Arguments}", piperCommand, startInfo.Arguments);

        using Process process = new Process { StartInfo = startInfo };

        process.Start();

        // Write the text to the process’s standard input.
        await process.StandardInput.WriteLineAsync(text);
        process.StandardInput.Close();

        await process.WaitForExitAsync();

        return ttsOutputPath;
    }

    public async Task StreamAudioAsync(IVoiceChannel voiceChannel, string filePath)
    {
        // Retrieve the audio client and log initial information.
        var amiquinVoice = _voiceStateManager.GetAmiquinVoice(voiceChannel.GuildId);
        if (amiquinVoice is null)
        {
            _logger.LogError(
                "Failed to retrieve AmiquinVoice for voice channel {ChannelName} in guild {GuildName}",
                voiceChannel.Name, voiceChannel.Guild.Name);

            return;
        }

        var audioClient = amiquinVoice.AudioClient;
        _logger.LogInformation(
            "Streaming audio to voice channel {ChannelName} in guild {GuildName}",
            voiceChannel.Name, voiceChannel.Guild.Name);

        if (audioClient is null)
        {
            _logger.LogError(
                "Failed to stream audio to voice channel {ChannelName} in guild {GuildName}",
                voiceChannel.Name, voiceChannel.Guild.Name);

            return;
        }

        _logger.LogInformation("AudioClient state: {State}", audioClient.ConnectionState);

        // Create the ffmpeg process using your dedicated method.
        using var ffmpeg = _voiceStateManager.CreateFfmpegProcess(filePath);
        if (ffmpeg is null)
        {
            _logger.LogError("Failed to create FFmpeg process for audio path {AudioPath}", filePath);
            return;
        }

        try
        {
            ffmpeg.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting FFmpeg process for audio path {AudioPath}", filePath);
            return;
        }

        using var ffmpegOutputStream = ffmpeg.StandardOutput.BaseStream;
        if (ffmpegOutputStream is null)
        {
            _logger.LogError("FFmpeg output stream is null for audio path {AudioPath}", filePath);
            return;
        }

        // Create a new PCM stream each time to avoid reusing a stale stream.
        try
        {
            if (amiquinVoice.AudioOutStream is null)
            {
                amiquinVoice.AudioOutStream = audioClient.CreatePCMStream(AudioApplication.Voice);
            }

            if (amiquinVoice.AudioOutStream is null)
            {
                _logger.LogError(
                    "Failed to create Discord audio stream for voice channel {ChannelName} in guild {GuildName}",
                    voiceChannel.Name, voiceChannel.Guild.Name);

                return;
            }

            try
            {
                await audioClient.SetSpeakingAsync(true);

                _logger.LogInformation(
                    "Starting audio streaming to voice channel {ChannelName} in guild {GuildName}",
                    voiceChannel.Name, voiceChannel.Guild.Name);

                // Stream audio from FFmpeg's output directly to Discord.
                await ffmpegOutputStream.CopyToAsync(amiquinVoice.AudioOutStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error during audio streaming to voice channel {ChannelName} in guild {GuildName}",
                    voiceChannel.Name, voiceChannel.Guild.Name);
            }
            finally
            {
                // Flush the Discord stream. Wrapping in a try-catch to handle any flush errors.
                try
                {
                    await amiquinVoice.AudioOutStream.FlushAsync();
                }
                catch (Exception flushEx)
                {
                    _logger.LogError(
                        flushEx,
                        "Error flushing Discord audio stream for voice channel {ChannelName}",
                        voiceChannel.Name);
                }
            }
        }
        finally
        {
            // Always clear the speaking state and wait for the FFmpeg process to exit.
            await audioClient.SetSpeakingAsync(false);

            ffmpeg.Close();

            _logger.LogInformation(
                "Finished streaming audio to voice channel {ChannelName} in guild {GuildName}",
                voiceChannel.Name, voiceChannel.Guild.Name);

            // Dispose of the FFmpeg process and output stream.
            ffmpeg.Dispose();
            ffmpegOutputStream.Dispose();
        }
    }

    public async Task JoinAsync(IVoiceChannel channel)
    {
        await _voiceStateManager.ConnectVoiceChannelAsync(channel);
        _logger.LogInformation("Joined voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
    }

    public async Task LeaveAsync(IVoiceChannel channel)
    {
        await _voiceStateManager.DisconnectVoiceChannelAsync(channel);
        _logger.LogInformation("Left voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
    }

    private void CreateRequiredDirectories()
    {
        if (!Directory.Exists(Constants.TTSBasePath))
        {
            Directory.CreateDirectory(Constants.TTSBasePath);
        }

        if (!Directory.Exists(Path.Join(Constants.TTSBasePath, "input")))
        {
            Directory.CreateDirectory(Path.Join(Constants.TTSBasePath, "input"));
        }

        if (!Directory.Exists(Path.Join(Constants.TTSBasePath, "output")))
        {
            Directory.CreateDirectory(Path.Join(Constants.TTSBasePath, "output"));
        }
    }
}