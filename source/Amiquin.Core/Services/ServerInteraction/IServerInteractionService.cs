using Discord.WebSocket;

namespace Amiquin.Core.Services.ServerInteraction;

public interface IServerInteractionService
{
    Task SendJoinMessageAsync(SocketGuild guild);
}