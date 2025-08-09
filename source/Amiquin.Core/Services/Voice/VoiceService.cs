using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ExternalProcessRunner;
using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Voice;

/// <summary>
/// Service implementation for managing voice operations in Discord channels.
/// Handles text-to-speech conversion, audio streaming, and voice channel management using external tools like Piper and FFmpeg.
/// </summary>
public class VoiceService : IVoiceService
{
    private readonly ILogger<VoiceService> _logger;
    private readonly IVoiceStateManager _voiceStateManager;
    private readonly IChatSemaphoreManager _chatSemaphoreManager;
    private readonly ExternalOptions _externalOptions;
    private readonly VoiceOptions _voiceOptions;
    private readonly IExternalProcessRunnerService _externalProcessRunner;

    /// <summary>
    /// Initializes a new instance of the VoiceService.
    /// </summary>
    /// <param name="logger">Logger instance for recording service operations.</param>
    /// <param name="voiceStateManager">Manager for handling voice channel state.</param>
    /// <param name="chatSemaphoreManager">Manager for handling voice operation synchronization.</param>
    /// <param name="externalOptions">Options for external tool configurations.</param>
    /// <param name="voiceOptions">Options for voice/TTS configurations.</param>
    /// <param name="externalProcessRunner">Service for running external processes like Piper and FFmpeg.</param>
    public VoiceService(ILogger<VoiceService> logger, IVoiceStateManager voiceStateManager, IChatSemaphoreManager chatSemaphoreManager, IOptions<ExternalOptions> externalOptions, IOptions<VoiceOptions> voiceOptions, IExternalProcessRunnerService externalProcessRunner)
    {
        _logger = logger;
        _voiceStateManager = voiceStateManager;
        _chatSemaphoreManager = chatSemaphoreManager;
        _externalOptions = externalOptions.Value;
        _voiceOptions = voiceOptions.Value;
        _externalProcessRunner = externalProcessRunner;
    }

    /// <inheritdoc/>
    public async Task SpeakAsync(IVoiceChannel voiceChannel, string text)
    {
        var filePath = await CreateTextToSpeechAudioAsync(text);

        var semaphore = _chatSemaphoreManager.GetOrCreateVoiceSemaphore(voiceChannel.GuildId);
        await semaphore.WaitAsync();
        try
        {
            await StreamAudioAsync(voiceChannel, filePath);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<string> CreateTextToSpeechAudioAsync(string text)
    {
        _logger.LogInformation("Creating text to speech audio for text: {Text}", text);

        var guid = Guid.NewGuid().ToString();

        string? modelName = _voiceOptions.TTSModelName;
        string? piperCommand = _voiceOptions.PiperCommand;

        var modelPath = Path.Join(Constants.Paths.TTSBasePath, string.IsNullOrEmpty(modelName) ? "en_GB-northern_english_male-medium.onnx" : $"{modelName}.onnx");
        var ttsOutputPath = Path.Join(Constants.Paths.TTSBasePath, "output", $"o_{guid}.wav");

        CreateRequiredDirectories();

        using var process = _externalProcessRunner.CreatePiperProcess(piperCommand, modelPath, ttsOutputPath);

        process.Start();

        // Write the text to the process’s standard input.
        await process.StandardInput.WriteLineAsync(text);
        process.StandardInput.Close();

        await process.WaitForExitAsync();

        return ttsOutputPath;
    }

    /// <inheritdoc/>
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
        using var ffmpeg = _externalProcessRunner.CreateFfmpegProcess(filePath);
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

    /// <inheritdoc/>
    public async Task JoinAsync(IVoiceChannel channel)
    {
        await _voiceStateManager.ConnectVoiceChannelAsync(channel);
        _logger.LogInformation("Joined voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
    }

    /// <inheritdoc/>
    public async Task LeaveAsync(IVoiceChannel channel)
    {
        await _voiceStateManager.DisconnectVoiceChannelAsync(channel);
        _logger.LogInformation("Left voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
    }

    /// <summary>
    /// Creates the required directories for TTS operations if they don't exist.
    /// </summary>
    private void CreateRequiredDirectories()
    {
        if (!Directory.Exists(Constants.Paths.TTSBasePath))
        {
            Directory.CreateDirectory(Constants.Paths.TTSBasePath);
        }

        if (!Directory.Exists(Path.Join(Constants.Paths.TTSBasePath, "output")))
        {
            Directory.CreateDirectory(Path.Join(Constants.Paths.TTSBasePath, "output"));
        }
    }
}