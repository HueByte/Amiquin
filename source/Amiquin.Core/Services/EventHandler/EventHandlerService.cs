using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.Meta;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="EventHandlerService"/> class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="commandHandlerService">The service for handling commands.</param>
    /// <param name="serviceScopeFactory">The factory for creating service scopes.</param>
    public EventHandlerService(ILogger<EventHandlerService> logger, ICommandHandlerService commandHandlerService, IServiceScopeFactory serviceScopeFactory, IChatContextService chatContextService)
    {
        _logger = logger;
        _commandHandlerService = commandHandlerService;
        _serviceScopeFactory = serviceScopeFactory;
        _chatContextService = chatContextService;
    }

    /// <inheritdoc/>
    public Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        _logger.LogInformation($"Shard {shard.ShardId} is ready");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task OnMessageReceivedAsync(SocketMessage message)
    {
        var guildId = (message.Channel as SocketGuildChannel)?.Guild.Id ?? 0;
        await _chatContextService.HandleUserMessageAsync(guildId, message).ConfigureAwait(false);

        _logger.LogInformation("Message received from [{user}] in [{channel}]: {content}", message.Author.Username, message.Channel.Name, message.Content);
    }

    /// <inheritdoc/>
    public async Task OnCommandCreatedAsync(SocketInteraction interaction)
    {
        await _commandHandlerService.HandleCommandAsync(interaction);
    }

    /// <inheritdoc/>
    public async Task OnShashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result)
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

        await serverMeta.CreateServerMetaAsync(guild.Id, guild.Name);

        if (await toggleService.IsEnabledAsync(guild.Id, Constants.ToggleNames.EnableJoinMessage))
        {
            await serverInteractionService.SendJoinMessageAsync(guild);
        }
    }
}