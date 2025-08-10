using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Core.Services.EventHandler;

/// <summary>
/// Service interface for handling Discord bot events and interactions.
/// Provides methods for managing bot lifecycle events, commands, and guild interactions.
/// </summary>
public interface IEventHandlerService
{
    /// <summary>
    /// Handles the event when a Discord shard becomes ready.
    /// </summary>
    /// <param name="shard">The Discord socket client shard that became ready.</param>
    Task OnShardReadyAsync(DiscordSocketClient shard);

    /// <summary>
    /// Handles the event when a command interaction is created.
    /// </summary>
    /// <param name="interaction">The socket interaction that was created.</param>
    Task OnCommandCreatedAsync(SocketInteraction interaction);

    /// <summary>
    /// Handles the event when a message is received.
    /// </summary>
    /// <param name="message">The received message.</param>
    Task OnMessageReceivedAsync(SocketMessage message);

    /// <summary>
    /// Handles the event when a slash command is executed.
    /// </summary>
    /// <param name="slashCommandInfo">Information about the executed slash command.</param>
    /// <param name="interactionContext">The context of the interaction.</param>
    /// <param name="result">The result of the command execution.</param>
    Task OnShashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result);

    /// <summary>
    /// Handles Discord client log messages.
    /// </summary>
    /// <param name="logMessage">The log message from the Discord client.</param>
    Task OnClientLogAsync(LogMessage logMessage);

    /// <summary>
    /// Handles the event when the bot joins a new guild.
    /// </summary>
    /// <param name="guild">The guild that the bot joined.</param>
    Task OnBotJoinedAsync(SocketGuild guild);

    /// <summary>
    /// Handles message component interactions (buttons, select menus, etc.).
    /// </summary>
    /// <param name="component">The message component interaction.</param>
    Task OnComponentInteractionAsync(SocketMessageComponent component);
}