using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Abstractions;

/// <summary>
/// Implementation of IDiscordClientWrapper that wraps DiscordShardedClient
/// </summary>
public class DiscordClientWrapper : IDiscordClientWrapper
{
    private readonly DiscordShardedClient _client;

    public DiscordClientWrapper(DiscordShardedClient client)
    {
        _client = client;
    }

    public SocketSelfUser? CurrentUser => _client.CurrentUser;

    public SocketGuild? GetGuild(ulong id) => _client.GetGuild(id);
}