using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Core.Services.CommandHandler;

/// <summary>
/// Service interface for handling Discord bot commands and interactions.
/// Provides methods for command initialization, execution, and management.
/// </summary>
public interface ICommandHandlerService
{
    /// <summary>
    /// Initializes the command handler service and registers commands.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Handles incoming command interactions from Discord.
    /// </summary>
    /// <param name="interaction">The socket interaction to handle.</param>
    Task HandleCommandAsync(SocketInteraction interaction);

    /// <summary>
    /// Handles the completion of slash command execution.
    /// </summary>
    /// <param name="slashCommandInfo">Information about the executed slash command.</param>
    /// <param name="interactionContext">The context of the interaction.</param>
    /// <param name="result">The result of the command execution.</param>
    Task HandleSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result);

    /// <summary>
    /// Gets a read-only collection of command names that should be handled as ephemeral (visible only to the user).
    /// </summary>
    IReadOnlyCollection<string> EphemeralCommands { get; }

    /// <summary>
    /// Gets a read-only collection of all registered slash commands.
    /// </summary>
    IReadOnlyCollection<SlashCommandInfo> Commands { get; }
}