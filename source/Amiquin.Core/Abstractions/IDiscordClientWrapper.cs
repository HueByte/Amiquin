using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Abstractions;

/// <summary>
/// Wrapper interface for Discord client to enable easier testing
/// </summary>
public interface IDiscordClientWrapper
{
    /// <summary>
    /// Current user of the Discord client
    /// </summary>
    SocketSelfUser? CurrentUser { get; }
    
    /// <summary>
    /// Get a guild by ID
    /// </summary>
    SocketGuild? GetGuild(ulong id);
}

/// <summary>
/// Wrapper interface for Discord guild to enable easier testing
/// </summary>
public interface IDiscordGuildWrapper
{
    /// <summary>
    /// Guild name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Guild ID
    /// </summary>
    ulong Id { get; }
}