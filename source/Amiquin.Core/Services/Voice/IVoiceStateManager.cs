using System.Diagnostics;
using Amiquin.Core.Services.Voice.Models;
using Discord;

namespace Amiquin.Core.Services.Voice;

public interface IVoiceStateManager
{
    Task ConnectVoiceChannelAsync(IVoiceChannel channel);
    Task DisconnectVoiceChannelAsync(IVoiceChannel? channel);
    AmiquinVoice? GetAmiquinVoice(ulong guildId);
}
