using Amiquin.Core.Services.Voice;
using Discord;

namespace Amiquin.Tests.Services.Voice;

/// <summary>
/// Mock implementation of IVoiceService for testing purposes.
/// This helps verify that the interface contract is sound and can be implemented.
/// </summary>
public class MockVoiceService : IVoiceService
{
    private readonly Dictionary<ulong, IVoiceChannel> _connectedChannels = new();

    // Track method calls for verification in tests
    public int SpeakAsyncCallCount { get; private set; }
    public int CreateTextToSpeechAudioAsyncCallCount { get; private set; }
    public int JoinAsyncCallCount { get; private set; }
    public int LeaveAsyncCallCount { get; private set; }
    public int StreamAudioAsyncCallCount { get; private set; }

    // Track the last parameters used
    public IVoiceChannel? LastVoiceChannel { get; private set; }
    public string? LastText { get; private set; }
    public string? LastFilePath { get; private set; }

    public Task<string> CreateTextToSpeechAudioAsync(string text)
    {
        CreateTextToSpeechAudioAsyncCallCount++;
        LastText = text;

        // In a mock, we just return a fake file path
        return Task.FromResult($"fake/path/to/audio_{Guid.NewGuid()}.wav");
    }

    public Task JoinAsync(IVoiceChannel channel)
    {
        JoinAsyncCallCount++;
        LastVoiceChannel = channel;

        if (!_connectedChannels.ContainsKey(channel.GuildId))
        {
            _connectedChannels.Add(channel.GuildId, channel);
        }

        return Task.CompletedTask;
    }

    public Task LeaveAsync(IVoiceChannel channel)
    {
        LeaveAsyncCallCount++;
        LastVoiceChannel = channel;

        if (_connectedChannels.ContainsKey(channel.GuildId))
        {
            _connectedChannels.Remove(channel.GuildId);
        }

        return Task.CompletedTask;
    }

    public async Task SpeakAsync(IVoiceChannel voiceChannel, string text)
    {
        SpeakAsyncCallCount++;
        LastVoiceChannel = voiceChannel;
        LastText = text;

        // In a real service, this would create audio and stream it
        var audioPath = await CreateTextToSpeechAudioAsync(text);
        await StreamAudioAsync(voiceChannel, audioPath);
    }

    public Task StreamAudioAsync(IVoiceChannel voiceChannel, string filePath)
    {
        StreamAudioAsyncCallCount++;
        LastVoiceChannel = voiceChannel;
        LastFilePath = filePath;

        return Task.CompletedTask;
    }

    public bool IsConnectedTo(ulong guildId)
    {
        return _connectedChannels.ContainsKey(guildId);
    }

    public void Reset()
    {
        SpeakAsyncCallCount = 0;
        CreateTextToSpeechAudioAsyncCallCount = 0;
        JoinAsyncCallCount = 0;
        LeaveAsyncCallCount = 0;
        StreamAudioAsyncCallCount = 0;

        LastVoiceChannel = null;
        LastText = null;
        LastFilePath = null;

        _connectedChannels.Clear();
    }
}
