using Amiquin.Core.Services.Voice.Models;
using Discord;

namespace Amiquin.Core.Services.Voice;

/// <summary>
/// Manager interface for handling Discord voice channel state and connections.
/// Provides methods for connecting to, disconnecting from, and managing voice channels.
/// </summary>
public interface IVoiceStateManager
{
    /// <summary>
    /// Connects to the specified Discord voice channel.
    /// </summary>
    /// <param name="channel">The Discord voice channel to connect to.</param>
    Task ConnectVoiceChannelAsync(IVoiceChannel channel);

    /// <summary>
    /// Disconnects from the specified Discord voice channel.
    /// </summary>
    /// <param name="channel">The Discord voice channel to disconnect from. Can be null.</param>
    Task DisconnectVoiceChannelAsync(IVoiceChannel? channel);

    /// <summary>
    /// Retrieves the AmiquinVoice instance for the specified guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to get the voice instance for.</param>
    /// <returns>The AmiquinVoice instance if connected; otherwise, null.</returns>
    AmiquinVoice? GetAmiquinVoice(ulong guildId);
}