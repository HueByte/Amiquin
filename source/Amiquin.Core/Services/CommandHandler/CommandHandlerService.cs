using System.Reflection;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.ServerMeta;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.CommandHandler;

public class CommandHandlerService : ICommandHandlerService
{
    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordShardedClient _discordClient;
    private readonly InteractionService _interactionService;
    private HashSet<string> _ephemeralCommands = [];
    public IReadOnlyCollection<string> EphemeralCommands => _ephemeralCommands.ToList().AsReadOnly();
    public IReadOnlyCollection<SlashCommandInfo> Commands => _interactionService.SlashCommands.ToList().AsReadOnly();

    public CommandHandlerService(ILogger<ICommandHandlerService> logger, IServiceScopeFactory scopeFactory, IServiceProvider serviceProvider, DiscordShardedClient discordClient, InteractionService interactionService)
    {
        _logger = logger;
        _serviceScopeFactory = scopeFactory;
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
        _interactionService = interactionService;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Command Handler Service");
        _logger.LogInformation("Adding Modules");

        await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
        _ephemeralCommands = Reflection.GetAllEphemeralCommands();
    }

    public async Task HandleCommandAsync(SocketInteraction interaction)
    {
        ExtendedShardedInteractionContext? extendedContext = null;
        BotContextAccessor? botContext = null;
        try
        {
            var scope = _serviceScopeFactory.CreateAsyncScope();

            extendedContext = new ExtendedShardedInteractionContext(_discordClient, interaction, scope);
            await interaction.DeferAsync(IsEphemeralCommand(interaction));

            botContext = scope.ServiceProvider.GetRequiredService<BotContextAccessor>();
            IServerMetaService? serverMetaService = scope.ServiceProvider.GetRequiredService<IServerMetaService>();

            // Ensure no concurrency issues when retrieving or creating server meta
            var serverMeta = await serverMetaService.GetOrCreateServerMetaAsync(extendedContext);
            botContext.Initialize(extendedContext, serverMeta, scope.ServiceProvider.GetRequiredService<IConfiguration>());

            await _interactionService.ExecuteCommandAsync(extendedContext, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command");
            await interaction.ModifyOriginalResponseAsync((msg) => msg.Content = "An error occurred while processing your command. Please try again later.");

            if (extendedContext is not null)
                await extendedContext.DisposeAsync();

            botContext?.Finish();
        }
    }

    public async Task HandleSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result)
    {
        var extendedContext = interactionContext as ExtendedShardedInteractionContext;
        var scope = extendedContext is not null ? extendedContext.AsyncScope : _serviceScopeFactory.CreateAsyncScope();
        try
        {
            var commandLogRepository = scope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
            var botContextAccessor = scope.ServiceProvider.GetRequiredService<BotContextAccessor>();
            botContextAccessor.Finish();
            await LogCommandAsync(commandLogRepository, botContextAccessor, slashCommandInfo, result);

            if (!result.IsSuccess)
            {
                var embed = new EmbedBuilder().WithCurrentTimestamp()
                                              .WithColor(Color.Red);

                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        embed.WithTitle("Unmet Precondition");
                        embed.WithDescription(result.ErrorReason);
                        break;

                    case InteractionCommandError.UnknownCommand:
                        embed.WithTitle("Unknown command");
                        embed.WithDescription(result.ErrorReason);
                        break;

                    case InteractionCommandError.BadArgs:
                        embed.WithTitle($"Invalid number or arguments");
                        embed.WithDescription(result.ErrorReason);
                        break;

                    case InteractionCommandError.Exception:
                        embed.WithTitle("Command exception");
                        embed.WithDescription(result.ErrorReason);

                        if (result is ExecuteResult execResult)
                        {
                            _logger.LogError(execResult.Exception, "Command [{name}] failed to execute in [{serverName}]", slashCommandInfo.Name, interactionContext.Guild.Name);
                        }

                        break;

                    case InteractionCommandError.Unsuccessful:
                        embed.WithTitle("Command could not be executed");
                        embed.WithDescription(result.ErrorReason);
                        break;

                    default:
                        embed.WithTitle("Something went wrong");
                        embed.WithDescription(result.ErrorReason);
                        break;
                }

                await interactionContext.Interaction.ModifyOriginalResponseAsync((msg) => msg.Embed = embed.Build());
            }
        }
        finally
        {
            if (extendedContext is null) scope.Dispose();
            else await scope.DisposeAsync();
        }
    }

    private bool IsEphemeralCommand(SocketInteraction interaction)
    {
        if (interaction.Type != InteractionType.ApplicationCommand)
            return false;

        if (interaction is not SocketSlashCommand command)
            return false;

        // Amiquin supports one level of subcommands.
        string commandName = command.Data.Options.FirstOrDefault(op => op.Type == ApplicationCommandOptionType.SubCommand)?.Name ?? command.Data.Name;
        return _ephemeralCommands.Contains(commandName);
    }

    private async Task LogCommandAsync(ICommandLogRepository commandLogRepository, BotContextAccessor context, SlashCommandInfo slashCommandInfo, IResult result)
    {
        if (context?.ServerMeta is null)
        {
            _logger.LogWarning("ServerMeta is null for command [{command}]", slashCommandInfo.Name);
            return;
        }

        var executionTime = (context.FinishedAt - context.CreatedAt).Milliseconds;
        var logEntry = new Models.CommandLog
        {
            Command = slashCommandInfo.Name,
            CommandDate = context.CreatedAt,
            Duration = executionTime,
            ErrorMessage = result.ErrorReason,
            IsSuccess = result.IsSuccess,
            ServerId = context.ServerMeta.Id,
            Username = context?.Context?.User?.Username ?? string.Empty,
        };

        await commandLogRepository.AddAsync(logEntry);
        await commandLogRepository.SaveChangesAsync();
        _logger.LogInformation("Command [{command}] executed by [{username}] in server [{serverName}] and took {timeToExecute} ms to execute", logEntry.Command, logEntry.Username, context?.ServerMeta?.ServerName, executionTime);
    }
}