using Discord.WebSocket;

namespace Amiquin.Core.Services.ServerInteraction;

/// <summary>
/// Service interface for interacting with Discord servers.
/// Provides methods for sending messages when the bot joins a server.
/// </summary>
public interface IServerInteractionService
{
    /// <summary>
    /// Sends a welcome message when the bot joins a new Discord server.
    /// </summary>
    /// <param name="guild">The Discord guild (server) that the bot joined.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendJoinMessageAsync(SocketGuild guild);
}