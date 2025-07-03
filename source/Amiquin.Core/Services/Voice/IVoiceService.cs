using Discord;

namespace Amiquin.Core.Services.Voice;

/// <summary>
/// Service interface for managing voice operations in Discord channels.
/// Provides methods for text-to-speech, audio streaming, and voice channel management.
/// </summary>
public interface IVoiceService
{
    /// <summary>
    /// Converts text to speech and plays it in the specified voice channel.
    /// </summary>
    /// <param name="voiceChannel">The Discord voice channel to speak in.</param>
    /// <param name="text">The text to convert to speech and play.</param>
    Task SpeakAsync(IVoiceChannel voiceChannel, string text);

    /// <summary>
    /// Creates an audio file from text using text-to-speech conversion.
    /// </summary>
    /// <param name="text">The text to convert to speech.</param>
    /// <returns>The file path of the generated audio file.</returns>
    Task<string> CreateTextToSpeechAudioAsync(string text);

    /// <summary>
    /// Joins the specified voice channel.
    /// </summary>
    /// <param name="channel">The Discord voice channel to join.</param>
    Task JoinAsync(IVoiceChannel channel);

    /// <summary>
    /// Leaves the specified voice channel.
    /// </summary>
    /// <param name="channel">The Discord voice channel to leave.</param>
    Task LeaveAsync(IVoiceChannel channel);

    /// <summary>
    /// Streams an audio file to the specified voice channel.
    /// </summary>
    /// <param name="voiceChannel">The Discord voice channel to stream audio to.</param>
    /// <param name="filePath">The path to the audio file to stream.</param>
    Task StreamAudioAsync(IVoiceChannel voiceChannel, string filePath);
}