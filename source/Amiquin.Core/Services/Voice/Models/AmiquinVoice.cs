using Discord;
using Discord.Audio;

namespace Amiquin.Core.Services.Voice.Models;

public class AmiquinVoice
{
    public IVoiceChannel? VoiceChannel { get; set; }
    public IAudioClient? AudioClient { get; set; }
    public AudioOutStream? AudioOutStream { get; set; }
}