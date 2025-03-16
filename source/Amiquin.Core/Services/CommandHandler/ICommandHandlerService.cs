using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Core.Services.CommandHandler;

public interface ICommandHandlerService
{
    Task InitializeAsync();
    Task HandleCommandAsync(SocketInteraction interaction);
    Task HandleSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result);
    IReadOnlyCollection<string> EphemeralCommands { get; }
    IReadOnlyCollection<SlashCommandInfo> Commands { get; }
}