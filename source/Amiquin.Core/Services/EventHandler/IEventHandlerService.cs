using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Core.Services.EventHandler;

public interface IEventHandlerService
{
    Task OnShardReadyAsync(DiscordSocketClient shard);
    Task OnCommandCreatedAsync(SocketInteraction interaction);
    Task OnShashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result);
    Task OnClientLogAsync(LogMessage logMessage);
    Task OnBotJoinedAsync(SocketGuild guild);
}