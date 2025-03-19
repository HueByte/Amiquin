using System.Collections.Concurrent;
using Amiquin.Core.Services.Voice.Models;
using Discord;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Voice;


public class VoiceStateManager : IVoiceStateManager
{
    private readonly ILogger<VoiceStateManager> _logger;
    private readonly ConcurrentDictionary<ulong, AmiquinVoice> _voiceChannels = new();

    public VoiceStateManager(ILogger<VoiceStateManager> logger)
    {
        _logger = logger;
    }

    public AmiquinVoice? GetAmiquinVoice(ulong guildId)
    {
        return _voiceChannels.TryGetValue(guildId, out var amiquinVoice) ? amiquinVoice : null;
    }

    public async Task ConnectVoiceChannelAsync(IVoiceChannel channel)
    {
        if (_voiceChannels.ContainsKey(channel.GuildId))
        {
            _logger.LogWarning("Already connected to voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
            return;
        }

        AmiquinVoice amiquinVoice = new()
        {
            VoiceChannel = channel,
            AudioClient = await channel.ConnectAsync()
        };

        AttachVoiceEvents(amiquinVoice, channel);

        var connectionResult = _voiceChannels.TryAdd(channel.GuildId, amiquinVoice);
        if (amiquinVoice.AudioClient is null || !connectionResult)
        {
            _logger.LogError("Failed to connect to voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
            return;
        }
    }

    public async Task DisconnectVoiceChannelAsync(IVoiceChannel? channel)
    {
        if (channel == null)
        {
            _logger.LogWarning("Provided channel is null");
            return;
        }

        if (_voiceChannels.TryRemove(channel.GuildId, out var amiquinVoice))
        {
            try
            {
                await channel.DisconnectAsync();
            }
            finally
            {
                amiquinVoice.AudioClient?.Dispose();
                amiquinVoice.AudioOutStream?.Dispose();
            }

            _logger.LogInformation("Left voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
        }
        else
        {
            _logger.LogWarning("Tried to leave voice channel {ChannelName} in guild {GuildName} but not connected", channel?.Name, channel?.Guild.Name);
        }
    }
    private void AttachVoiceEvents(AmiquinVoice amiquinVoice, IVoiceChannel channel)
    {
        if (amiquinVoice.AudioClient is null)
        {
            _logger.LogError("Audio client is null");
            return;
        }

        var channelName = channel.Name;
        var guildName = channel.Guild.Name;

        amiquinVoice.AudioClient.UdpLatencyUpdated += (oldLatency, newLatency) =>
        {
            _logger.LogInformation("UDP latency updated from {OldLatency} to {NewLatency} in voice channel {ChannelName} in guild {GuildName}", oldLatency, newLatency, channelName, guildName);

            return Task.CompletedTask;
        };

        amiquinVoice.AudioClient.StreamCreated += (id, audioInStream) =>
        {
            _logger.LogInformation("Stream created in voice channel {ChannelName} ~ Length {Length}", channelName, audioInStream.Length);

            return Task.CompletedTask;
        };

        amiquinVoice.AudioClient.StreamDestroyed += (id) =>
        {
            _logger.LogInformation("Stream destroyed in voice channel {ChannelName}", channelName);

            return Task.CompletedTask;
        };

        amiquinVoice.AudioClient.Connected += () =>
        {
            _logger.LogInformation("Connected to voice channel {ChannelName} in guild {GuildName}", channelName, guildName);

            return Task.CompletedTask;
        };

        amiquinVoice.AudioClient.Disconnected += async (exception) =>
        {
            _logger.LogInformation("Disconnected from voice channel {ChannelName} in guild {GuildName} ~ Exception {Exception}", channelName, guildName, exception);

            await DisconnectVoiceChannelAsync(channel);
        };

        amiquinVoice.AudioClient.ClientDisconnected += (userId) =>
        {
            _logger.LogInformation("User disconnected from voice channel {ChannelName} in guild {GuildName} ~ User {UserId}", channelName, guildName, userId);

            return Task.CompletedTask;
        };
    }
}