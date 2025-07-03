using Amiquin.Core.Services.Voice.Models;
using Discord;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.Voice;

/// <summary>
/// Implementation of the <see cref="IVoiceStateManager"/> interface.
/// Manages Discord voice channel connections and state.
/// </summary>
public class VoiceStateManager : IVoiceStateManager
{
    private readonly ILogger<VoiceStateManager> _logger;
    private readonly ConcurrentDictionary<ulong, AmiquinVoice> _voiceChannels = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceStateManager"/> class.
    /// </summary>
    /// <param name="logger">The logger for this manager.</param>
    public VoiceStateManager(ILogger<VoiceStateManager> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public AmiquinVoice? GetAmiquinVoice(ulong guildId)
    {
        return _voiceChannels.TryGetValue(guildId, out var amiquinVoice) ? amiquinVoice : null;
    }

    /// <inheritdoc/>
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

        var streams = amiquinVoice.AudioClient.GetStreams();
        foreach (var (streamId, stream) in streams)
        {
            _logger.LogInformation("Stream {StreamId} ~ Length {Length}", streamId, stream.AvailableFrames);
        }

        _ = Task.Run(async () =>
        {
            var streamId = streams.Keys.FirstOrDefault(); // Replace with the desired stream ID if known
            if (streamId == default || !streams.TryGetValue(streamId, out var stream))
            {
                _logger.LogWarning("No valid stream found to process");
                return;
            }

            while (true)
            {
                if (stream.AvailableFrames > 0)
                {
                    var buffer = new byte[stream.AvailableFrames];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        _logger.LogInformation("Received {BytesRead} bytes from Stream {StreamId}", bytesRead, streamId);
                    }
                }

                await Task.Delay(10); // Add a small delay to prevent excessive CPU usage
            }
        });

        AttachVoiceEvents(amiquinVoice, channel);

        var connectionResult = _voiceChannels.TryAdd(channel.GuildId, amiquinVoice);
        if (amiquinVoice.AudioClient is null || !connectionResult)
        {
            _logger.LogError("Failed to connect to voice channel {ChannelName} in guild {GuildName}", channel.Name, channel.Guild.Name);
            return;
        }
    }

    /// <inheritdoc/>
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
    /// <summary>
    /// Attaches event handlers to the voice client events.
    /// </summary>
    /// <param name="amiquinVoice">The voice client wrapper.</param>
    /// <param name="channel">The Discord voice channel.</param>
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