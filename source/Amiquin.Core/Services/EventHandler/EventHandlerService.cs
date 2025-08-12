using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Modal;
using Amiquin.Core.Services.ServerInteraction;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.EventHandler;

/// <summary>
/// Implementation of the <see cref="IEventHandlerService"/> interface.
/// Handles Discord bot events and interactions.
/// </summary>
public class EventHandlerService : IEventHandlerService
{
    private readonly ILogger _logger;
    private readonly ICommandHandlerService _commandHandlerService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IChatContextService _chatContextService;
    private readonly IComponentHandlerService _componentHandlerService;
    private readonly IModalService _modalService;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventHandlerService"/> class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="commandHandlerService">The service for handling commands.</param>
    /// <param name="serviceScopeFactory">The factory for creating service scopes.</param>
    /// <param name="chatContextService">The service for handling chat context.</param>
    /// <param name="componentHandlerService">The service for handling component interactions.</param>
    /// <param name="modalService">The service for handling modal interactions.</param>
    public EventHandlerService(ILogger<EventHandlerService> logger, ICommandHandlerService commandHandlerService, IServiceScopeFactory serviceScopeFactory, IChatContextService chatContextService, IComponentHandlerService componentHandlerService, IModalService modalService)
    {
        _logger = logger;
        _commandHandlerService = commandHandlerService;
        _serviceScopeFactory = serviceScopeFactory;
        _chatContextService = chatContextService;
        _componentHandlerService = componentHandlerService;
        _modalService = modalService;
    }

    /// <inheritdoc/>
    public Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        _logger.LogInformation($"Shard {shard.ShardId} is ready");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnMessageReceivedAsync(SocketMessage message)
    {
        _ = Task.Run(() => OnMessageReceivedInnerAsync(message).ConfigureAwait(false)).ConfigureAwait(false);

        _logger.LogInformation("Message received from [{user}] in [{channel}]: {content}", message.Author.Username, message.Channel.Name, message.Content);

        return Task.CompletedTask;
    }

    public async Task OnMessageReceivedInnerAsync(SocketMessage message)
    {
        var guildId = (message.Channel as SocketGuildChannel)?.Guild.Id ?? 0;

        // Handle the message for context tracking first
        await _chatContextService.HandleUserMessageAsync(guildId, message).ConfigureAwait(false);

        // Check if bot was mentioned and respond immediately
        if (guildId > 0 && !message.Author.IsBot && !string.IsNullOrWhiteSpace(message.Content))
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetService<DiscordShardedClient>();
            var toggleService = scope.ServiceProvider.GetService<IToggleService>();

            if (discordClient?.CurrentUser != null && toggleService != null)
            {
                // Check if chat is enabled for this server
                var isChatEnabled = await toggleService.IsEnabledAsync(guildId, Constants.ToggleNames.EnableChat);
                if (!isChatEnabled)
                {
                    return; // Don't respond to mentions if chat is disabled
                }

                var botUserId = discordClient.CurrentUser.Id;
                var isMentioned = message.MentionedUserIds.Contains(botUserId) ||
                                 message.Content.Contains("@Amiquin", StringComparison.OrdinalIgnoreCase);

                if (isMentioned)
                {
                    _logger.LogInformation("Bot was mentioned by {Username} in guild {GuildId}, responding immediately",
                        message.Author.Username, guildId);

                    // Use the AnswerMentionAsync method for immediate response
                    var response = await _chatContextService.AnswerMentionAsync(guildId, message);

                    if (string.IsNullOrEmpty(response))
                    {
                        _logger.LogWarning("Failed to generate mention response for guild {GuildId}", guildId);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully responded to mention in guild {GuildId}", guildId);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task OnCommandCreatedAsync(SocketInteraction interaction)
    {
        _logger.LogInformation("Received interaction of type {InteractionType} with ID {InteractionId}", interaction.Type, interaction.Id);
        _logger.LogInformation("Is SocketMessageComponent: {IsMessageComponent}, Is SocketModal: {IsModal}",
            interaction is SocketMessageComponent, interaction is SocketModal);

        // Handle component interactions (buttons, select menus, etc.)
        if (interaction is SocketMessageComponent component)
        {
            await OnComponentInteractionAsync(component);
            return;
        }

        // Handle modal interactions
        if (interaction is SocketModal modal)
        {
            // Modal submissions should be handled directly by the command handler
            // which will process [ModalInteraction] attributes through Discord.Net's system
            await _commandHandlerService.HandleCommandAsync(interaction);
            return;
        }

        // Handle other interactions (slash commands, etc.)
        await _commandHandlerService.HandleCommandAsync(interaction);
    }

    /// <inheritdoc/>
    public async Task OnSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result)
    {
        _logger.LogInformation("Command [{name}] created by [{user}] in [{server_name}]", slashCommandInfo.Name, interactionContext.User.Username, interactionContext.Guild.Name);
        await _commandHandlerService.HandleSlashCommandExecutedAsync(slashCommandInfo, interactionContext, result);
    }

    /// <inheritdoc/>
    public async Task OnClientLogAsync(LogMessage logMessage)
    {
        // Due to the nature of Discord.Net's event system, all log event handlers will be executed synchronously on the gateway thread.
        // Using Task.Run so the gateway thread does not become blocked while waiting for logging data to be written.
        _ = Task.Run(() =>
        {
            switch (logMessage.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    _logger.LogError(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Warning:

                    _logger.LogWarning(logMessage.Exception, logMessage.Message);
                    break;
                case LogSeverity.Info:
                    _logger.LogInformation(logMessage.Message);
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    _logger.LogDebug(logMessage.Message);
                    break;
            }
        });

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task OnBotJoinedAsync(SocketGuild guild)
    {
        _logger.LogInformation("Bot joined guild [{guild_name}]", guild.Name);

        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var serverInteractionService = scope.ServiceProvider.GetRequiredService<IServerInteractionService>();
        var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();
        var serverMeta = scope.ServiceProvider.GetRequiredService<IServerMetaService>();

        // Create or update server metadata
        await serverMeta.CreateServerMetaAsync(guild.Id, guild.Name);
        
        // Ensure toggles exist for this server (this preserves existing toggle values)
        await toggleService.CreateServerTogglesIfNotExistsAsync(guild.Id);

        if (await toggleService.IsEnabledAsync(guild.Id, Constants.ToggleNames.EnableJoinMessage))
        {
            await serverInteractionService.SendJoinMessageAsync(guild);
        }
    }

    /// <inheritdoc/>
    public async Task OnComponentInteractionAsync(SocketMessageComponent component)
    {
        // Check if this interaction will trigger a modal (which cannot be deferred)
        var customId = component.Data.CustomId;
        bool isModalTrigger = _componentHandlerService.WillTriggerModal(customId);

        // Only defer if this is not a modal trigger interaction
        if (!isModalTrigger)
        {
            try
            {
                await component.DeferAsync();
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownInteraction)
            {
                _logger.LogWarning("Component interaction {InteractionId} expired before defer (10062)", component.Id);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to defer component interaction {InteractionId}", component.Id);
                return;
            }
        }

        try
        {
            var handled = await _componentHandlerService.HandleInteractionAsync(component);
            if (!handled)
            {
                _logger.LogDebug("Component interaction {CustomId} not handled by any registered service", component.Data.CustomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling component interaction {CustomId}", component.Data.CustomId);
            try
            {
                if (component.HasResponded)
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "An error occurred while processing your interaction.");
                else if (component.HasResponded)
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "An error occurred while processing your interaction.");
                else
                    await component.RespondAsync("An error occurred while processing your interaction.", ephemeral: true);
            }
            catch
            {
                // Ignore errors when responding to the user about errors
            }
        }
    }

    /// <summary>
    /// Handles modal submissions by routing them to the appropriate service.
    /// </summary>
    /// <param name="modal">The modal submission to handle.</param>
    public async Task OnModalSubmissionAsync(SocketModal modal)
    {
        // Defer immediately to avoid 3-second timeout
        try
        {
            await modal.DeferAsync();
        }
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == DiscordErrorCode.UnknownInteraction)
        {
            _logger.LogWarning("Modal submission {InteractionId} expired before defer (10062)", modal.Id);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to defer modal submission {InteractionId}", modal.Id);
            return;
        }

        try
        {
            var handled = await _modalService.HandleModalSubmissionAsync(modal);
            if (!handled)
            {
                _logger.LogDebug("Modal submission {CustomId} not handled by any registered service", modal.Data.CustomId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modal submission {CustomId}", modal.Data.CustomId);
            try
            {
                await modal.ModifyOriginalResponseAsync(msg => msg.Content = "An error occurred while processing your submission.");
            }
            catch
            {
                // Ignore errors when responding to the user about errors
            }
        }
    }

    /// <inheritdoc/>
    public async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            _logger.LogInformation("User {Username} ({UserId}) joined guild {GuildName} ({GuildId})",
                user.Username, user.Id, user.Guild.Name, user.Guild.Id);

            using var scope = _serviceScopeFactory.CreateScope();
            var toggleService = scope.ServiceProvider.GetRequiredService<IToggleService>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();
            var serverMetaService = scope.ServiceProvider.GetRequiredService<IServerMetaService>();

            // Check if AI welcome is enabled for this server
            var isAIWelcomeEnabled = await toggleService.IsEnabledAsync(user.Guild.Id, Constants.ToggleNames.EnableAIWelcome);
            if (!isAIWelcomeEnabled)
            {
                _logger.LogDebug("AI welcome disabled for guild {GuildId}", user.Guild.Id);
                return;
            }

            // Get the primary channel for the guild
            var serverMeta = await serverMetaService.GetServerMetaAsync(user.Guild.Id);
            IMessageChannel? welcomeChannel = null;

            if (serverMeta?.PrimaryChannelId.HasValue == true)
            {
                welcomeChannel = user.Guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            }

            // Fall back to the first general/welcome channel if no primary channel set
            if (welcomeChannel == null)
            {
                var generalChannel = user.Guild.TextChannels
                    .Where(c => user.Guild.CurrentUser.GetPermissions(c).SendMessages &&
                               user.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                    .Where(c => c.Name.Contains("general") || c.Name.Contains("welcome") || c.Name.Contains("chat"))
                    .OrderBy(c => c.Position)
                    .FirstOrDefault();

                welcomeChannel = generalChannel ?? user.Guild.TextChannels
                    .Where(c => user.Guild.CurrentUser.GetPermissions(c).SendMessages &&
                               user.Guild.CurrentUser.GetPermissions(c).ViewChannel)
                    .OrderBy(c => c.Position)
                    .FirstOrDefault();
            }

            if (welcomeChannel == null)
            {
                _logger.LogWarning("No suitable welcome channel found for guild {GuildId}", user.Guild.Id);
                return;
            }

            // Create AI-powered welcome message using the chat service
            var welcomePrompt = $"[System]: A new user named {user.DisplayName} (username: {user.Username}) just joined the server '{user.Guild.Name}'. " +
                               $"Generate a warm, friendly welcome message for them. Keep it conversational and welcoming, " +
                               $"and mention their name. Make it feel like a genuine community welcome from the server's AI assistant.";

            var welcomeMessage = await personaChatService.ChatAsync(
                user.Guild.Id, 
                user.Guild.CurrentUser.Id, 
                user.Guild.CurrentUser.Id, 
                welcomePrompt);

            if (!string.IsNullOrWhiteSpace(welcomeMessage))
            {
                // Send the AI-generated welcome message
                await welcomeChannel.SendMessageAsync(welcomeMessage);
                _logger.LogInformation("Sent AI welcome message for user {UserId} in guild {GuildId}", user.Id, user.Guild.Id);
            }
            else
            {
                _logger.LogWarning("Failed to generate AI welcome message for user {UserId} in guild {GuildId}", user.Id, user.Guild.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling user join event for user {UserId} in guild {GuildId}", user.Id, user.Guild.Id);
        }
    }
}