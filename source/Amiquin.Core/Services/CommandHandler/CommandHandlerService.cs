using System.Reflection;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
    private HashSet<string> _ephemeralCommands = new();
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
        try
        {
            var scope = _serviceScopeFactory.CreateAsyncScope();
            extendedContext = new ExtendedShardedInteractionContext(_discordClient, interaction, scope);

            await interaction.DeferAsync(IsEphemeralCommand(interaction));
            await _interactionService.ExecuteCommandAsync(extendedContext, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());

            _logger.LogError(ex, "Failed to execute command");
            if (extendedContext is not null)
                await extendedContext.DisposeAsync();
        }
    }

    public async Task HandleSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result)
    {
        var extendedContext = interactionContext as ExtendedShardedInteractionContext;
        var scope = extendedContext is not null ? extendedContext.AsyncScope : _serviceScopeFactory.CreateAsyncScope();
        try
        {
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

        var command = interaction as SocketSlashCommand;
        if (command is null)
            return false;

        // Amiquin supports one level of subcommands.
        string commandName = command.Data.Options.FirstOrDefault(op => op.Type == ApplicationCommandOptionType.SubCommand)?.Name ?? command.Data.Name;
        return _ephemeralCommands.Contains(commandName);
    }
}