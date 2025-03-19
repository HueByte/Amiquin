using Discord;

namespace Amiquin.Core.Services.Voice;

public interface IVoiceService
{
    Task SpeakAsync(IVoiceChannel voiceChannel, string text);
    Task<string> CreateTextToSpeechAudioAsync(string text);
    Task JoinAsync(IVoiceChannel channel);
    Task LeaveAsync(IVoiceChannel channel);
    Task StreamAudioAsync(IVoiceChannel voiceChannel, string filePath);
}
