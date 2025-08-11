using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Amiquin.Core.Services.CommandHandler;

/// <summary>
/// Implementation of the <see cref="ICommandHandlerService"/> interface.
/// Handles Discord bot commands and interactions with enhanced error handling and resource management.
/// </summary>
public class CommandHandlerService : ICommandHandlerService
{
    private const string DefaultErrorMessage = "An error occurred while processing your command. Please try again later.";
    private const string CommandExecutionLogTemplate = "Command [{command}] executed by [{username}] in server [{serverName}] and took {timeToExecute} ms to execute";
    private const string CommandFailedLogTemplate = "Command [{name}] failed to execute in [{serverName}]";
    private const string ServerMetaNullWarning = "ServerMeta is null for command [{command}]";
    private const string InitializingMessage = "Initializing Command Handler Service";
    private const string AddingModulesMessage = "Adding Modules";

    private readonly ILogger _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordShardedClient _discordClient;
    private readonly InteractionService _interactionService;
    private readonly HashSet<string> _ephemeralCommands = [];
    private readonly HashSet<string> _modalCommands = [];
    private volatile bool _isInitialized;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> EphemeralCommands => _ephemeralCommands.ToList().AsReadOnly();

    /// <inheritdoc/>
    public IReadOnlyCollection<SlashCommandInfo> Commands => _interactionService.SlashCommands.ToList().AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandHandlerService"/> class.
    /// </summary>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="scopeFactory">The factory for creating service scopes.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="discordClient">The Discord sharded client.</param>
    /// <param name="interactionService">The interaction service for handling Discord interactions.</param>
    public CommandHandlerService(ILogger<ICommandHandlerService> logger, IServiceScopeFactory scopeFactory, IServiceProvider serviceProvider, DiscordShardedClient discordClient, InteractionService interactionService)
    {
        _logger = logger;
        _serviceScopeFactory = scopeFactory;
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;
        _interactionService = interactionService;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Command Handler Service is already initialized");
            return;
        }

        try
        {
            _logger.LogInformation(InitializingMessage);
            _logger.LogInformation(AddingModulesMessage);

            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);

            var ephemeralCommands = Reflection.GetAllEphemeralCommands();
            foreach (var command in ephemeralCommands)
            {
                _ephemeralCommands.Add(command);
            }

            var modalCommands = Reflection.GetAllModalCommands();
            foreach (var command in modalCommands)
            {
                _modalCommands.Add(command);
            }

            _isInitialized = true;
            _logger.LogInformation("Command Handler Service initialized successfully with {ephemeralCommandCount} ephemeral commands and {modalCommandCount} modal commands", _ephemeralCommands.Count, _modalCommands.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Command Handler Service");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task HandleCommandAsync(SocketInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (!_isInitialized)
        {
            _logger.LogWarning("Command Handler Service is not initialized, cannot process command");
            await RespondWithErrorAsync(interaction, "Service is not ready. Please try again later.");
            return;
        }

        bool isModal = IsModalCommand(interaction);
        bool isModalSubmission = interaction is SocketModal;

        // Handle autocomplete interactions separately - they don't support DeferAsync/RespondAsync like regular interactions
        if (interaction is SocketAutocompleteInteraction)
        {
            try
            {
                var scope = _serviceScopeFactory.CreateAsyncScope();
                var autocompleteContext = new ExtendedShardedInteractionContext(_discordClient, interaction, scope);

                _logger.LogDebug("Executing autocomplete interaction for user {userId} in guild {guildId}",
                    interaction.User.Id, GetGuildId(interaction));

                await _interactionService.ExecuteCommandAsync(autocompleteContext, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute autocomplete interaction for user {userId} in guild {guildId}",
                    interaction.User.Id, GetGuildId(interaction));
            }
            return;
        }
        // Don't defer modal commands or modal submissions - they need to respond immediately
        else if (!isModal && !isModalSubmission)
        {
            bool isEphemeral = IsEphemeralCommand(interaction);
            await interaction.DeferAsync(isEphemeral);
        }

        ExtendedShardedInteractionContext? extendedContext = null;

        try
        {
            var scope = _serviceScopeFactory.CreateAsyncScope();
            extendedContext = new ExtendedShardedInteractionContext(_discordClient, interaction, scope);

            var botContext = scope.ServiceProvider.GetRequiredService<BotContextAccessor>();
            var serverMetaService = scope.ServiceProvider.GetRequiredService<IServerMetaService>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // Ensure no concurrency issues when retrieving or creating server meta
            var serverMeta = await serverMetaService.GetOrCreateServerMetaAsync(extendedContext);
            botContext.Initialize(extendedContext, serverMeta, configuration);

            var guildId = GetGuildId(interaction);
            _logger.LogDebug("Executing command {commandName} for user {userId} in guild {guildId}",
                GetCommandName(interaction), interaction.User.Id, guildId);

            var result = await _interactionService.ExecuteCommandAsync(extendedContext, scope.ServiceProvider);
            _logger.LogDebug("Interaction execution result: Success={IsSuccess}, Error={Error}, ErrorReason={ErrorReason}", 
                result.IsSuccess, result.Error, result.ErrorReason);

            // Command executed - BotContextAccessor will be finished in HandleSlashCommandExecuted regardless of success/failure
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command {commandName} for user {userId} in guild {guildId}",
                GetCommandName(interaction), interaction.User.Id, GetGuildId(interaction));

            await HandleCommandErrorAsync(interaction, ex);

            // On exception in HandleCommandAsync, we need to clean up ourselves since HandleSlashCommandExecuted won't be called
            if (extendedContext is not null)
            {
                try
                {
                    // Finish the BotContextAccessor before disposing the scope
                    var botContext = extendedContext.AsyncScope.ServiceProvider.GetRequiredService<BotContextAccessor>();
                    botContext.Finish();

                    await extendedContext.DisposeAsync();
                }
                catch (Exception disposeEx)
                {
                    _logger.LogWarning(disposeEx, "Failed to dispose extended context after error");
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task HandleSlashCommandExecutedAsync(SlashCommandInfo slashCommandInfo, IInteractionContext interactionContext, IResult result)
    {
        ArgumentNullException.ThrowIfNull(slashCommandInfo);
        ArgumentNullException.ThrowIfNull(interactionContext);
        ArgumentNullException.ThrowIfNull(result);

        var extendedContext = interactionContext as ExtendedShardedInteractionContext;

        if (extendedContext is null)
        {
            _logger.LogWarning("InteractionContext is not ExtendedShardedInteractionContext for command {commandName}", slashCommandInfo.Name);
            return;
        }

        BotContextAccessor? botContextAccessor = null;

        try
        {
            var commandLogRepository = extendedContext.AsyncScope.ServiceProvider.GetRequiredService<ICommandLogRepository>();
            botContextAccessor = extendedContext.AsyncScope.ServiceProvider.GetRequiredService<BotContextAccessor>();

            // Mark context as finished - this should only be called once per command
            if (!botContextAccessor.IsFinished)
            {
                botContextAccessor.Finish();
            }
            else
            {
                _logger.LogWarning("BotContextAccessor for command {commandName} was already finished", slashCommandInfo.Name);
            }

            await LogCommandAsync(commandLogRepository, botContextAccessor, slashCommandInfo, result);

            if (!result.IsSuccess)
            {
                await HandleCommandExecutionErrorAsync(interactionContext, slashCommandInfo, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle slash command execution for {commandName}", slashCommandInfo.Name);

            // Ensure BotContextAccessor is finished even if there was an error
            if (botContextAccessor is not null && !botContextAccessor.IsFinished)
            {
                try
                {
                    botContextAccessor.Finish();
                }
                catch (Exception finishEx)
                {
                    _logger.LogWarning(finishEx, "Failed to finish BotContextAccessor for command {commandName}", slashCommandInfo.Name);
                }
            }
        }
        finally
        {
            // Dispose the extended context which will dispose the async scope
            try
            {
                await extendedContext.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispose extended context for command {commandName}", slashCommandInfo.Name);
            }
        }
    }

    /// <summary>
    /// Determines if the command should be executed in ephemeral mode (visible only to the command issuer).
    /// </summary>
    /// <param name="interaction">The socket interaction to check.</param>
    /// <returns>True if the command should be ephemeral; otherwise, false.</returns>
    private bool IsEphemeralCommand(SocketInteraction interaction)
    {
        if (interaction.Type != InteractionType.ApplicationCommand || interaction is not SocketSlashCommand command)
            return false;

        // Amiquin supports one level of subcommands.
        var commandName = command.Data.Options.FirstOrDefault(op => op.Type == ApplicationCommandOptionType.SubCommand)?.Name ?? command.Data.Name;
        return !string.IsNullOrEmpty(commandName) && _ephemeralCommands.Contains(commandName);
    }

    /// <summary>
    /// Determines if the interaction is a modal command that should not be deferred.
    /// Modal commands need to respond immediately with the modal, not defer first.
    /// Uses the IsModal attribute system to detect commands marked with [IsModal].
    /// </summary>
    /// <param name="interaction">The socket interaction to check.</param>
    /// <returns>True if this is a modal command that should not be deferred.</returns>
    private bool IsModalCommand(SocketInteraction interaction)
    {
        if (interaction.Type != InteractionType.ApplicationCommand || interaction is not SocketSlashCommand command)
            return false;

        // Get the actual command name - need to handle nested subcommand groups properly
        string commandName;
        
        // Check if there's a subcommand group (like "admin config setup")
        var firstOption = command.Data.Options?.FirstOrDefault();
        if (firstOption?.Type == ApplicationCommandOptionType.SubCommandGroup)
        {
            // This is a subcommand group - get the actual subcommand within it
            var subCommand = firstOption.Options?.FirstOrDefault(op => op.Type == ApplicationCommandOptionType.SubCommand);
            commandName = subCommand?.Name ?? firstOption.Name;
        }
        else if (firstOption?.Type == ApplicationCommandOptionType.SubCommand)
        {
            // This is a direct subcommand
            commandName = firstOption.Name;
        }
        else
        {
            // This is a top-level command
            commandName = command.Data.Name;
        }
        
        var isModal = !string.IsNullOrEmpty(commandName) && _modalCommands.Contains(commandName);
        
        _logger.LogDebug("Checking modal command: '{CommandName}' -> IsModal: {IsModal} (Full structure: {FullCommand})", 
            commandName, isModal, GetFullSlashCommandName(command));
        return isModal;
    }

    /// <summary>
    /// Logs command execution information to the database.
    /// </summary>
    /// <param name="commandLogRepository">The repository for command logs.</param>
    /// <param name="context">The bot context accessor containing context information.</param>
    /// <param name="slashCommandInfo">Information about the executed slash command.</param>
    /// <param name="result">The result of the command execution.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task LogCommandAsync(ICommandLogRepository commandLogRepository, BotContextAccessor context, SlashCommandInfo slashCommandInfo, IResult result)
    {
        try
        {
            if (context?.ServerMeta is null)
            {
                _logger.LogWarning(ServerMetaNullWarning, slashCommandInfo.Name);
                return;
            }

            var executionTime = (int)(context.FinishedAt - context.CreatedAt).TotalMilliseconds;
            var logEntry = new Models.CommandLog
            {
                Command = slashCommandInfo.Name,
                CommandDate = context.CreatedAt,
                Duration = executionTime,
                ErrorMessage = result.ErrorReason,
                IsSuccess = result.IsSuccess,
                ServerId = context.ServerMeta.Id,
                Username = context.Context?.User?.Username ?? "Unknown",
            };

            await commandLogRepository.AddAsync(logEntry);
            await commandLogRepository.SaveChangesAsync();

            _logger.LogInformation(CommandExecutionLogTemplate,
                logEntry.Command,
                logEntry.Username,
                context.ServerMeta.ServerName,
                executionTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log command execution for {commandName}", slashCommandInfo.Name);
        }
    }

    /// <summary>
    /// Handles command errors with appropriate responses to the user.
    /// </summary>
    /// <param name="interaction">The interaction that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    private async Task HandleCommandErrorAsync(SocketInteraction interaction, Exception exception)
    {
        try
        {
            // Special handling for modal-related errors
            if (exception is InvalidOperationException invalidOp &&
                invalidOp.Message.Contains("Cannot respond twice to the same interaction"))
            {
                _logger.LogDebug("Command appears to be a modal command that was incorrectly deferred. " +
                                "Interaction {InteractionId} - this is expected behavior for modal commands.",
                                interaction.Id);
                return; // Don't try to respond, the modal should have been shown successfully
            }

            var errorMessage = exception switch
            {
                TimeoutException => "The command timed out. Please try again.",
                UnauthorizedAccessException => "You don't have permission to execute this command.",
                InvalidOperationException => "The command cannot be executed in this context.",
                ArgumentException => "Invalid arguments provided to the command.",
                _ => DefaultErrorMessage
            };

            await RespondWithErrorAsync(interaction, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle command error for interaction {interactionId}", interaction.Id);
        }
    }

    /// <summary>
    /// Handles command execution errors with appropriate error embeds.
    /// </summary>
    /// <param name="interactionContext">The interaction context.</param>
    /// <param name="slashCommandInfo">The slash command information.</param>
    /// <param name="result">The execution result containing error information.</param>
    private async Task HandleCommandExecutionErrorAsync(IInteractionContext interactionContext, SlashCommandInfo slashCommandInfo, IResult result)
    {
        try
        {
            var embed = CreateErrorEmbed(result);

            if (result.Error == InteractionCommandError.Exception && result is ExecuteResult execResult)
            {
                _logger.LogError(execResult.Exception, CommandFailedLogTemplate,
                    slashCommandInfo.Name,
                    interactionContext.Guild?.Name ?? "DM");
            }

            await interactionContext.Interaction.ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle command execution error for {commandName}", slashCommandInfo.Name);
        }
    }

    /// <summary>
    /// Creates an error embed based on the command execution result.
    /// </summary>
    /// <param name="result">The command execution result.</param>
    /// <returns>An EmbedBuilder configured with error information.</returns>
    private static EmbedBuilder CreateErrorEmbed(IResult result)
    {
        var embed = new EmbedBuilder()
            .WithCurrentTimestamp()
            .WithColor(Color.Red);

        return result.Error switch
        {
            InteractionCommandError.UnmetPrecondition => embed
                .WithTitle("Unmet Precondition")
                .WithDescription(result.ErrorReason),
            InteractionCommandError.UnknownCommand => embed
                .WithTitle("Unknown Command")
                .WithDescription(result.ErrorReason),
            InteractionCommandError.BadArgs => embed
                .WithTitle("Invalid Arguments")
                .WithDescription(result.ErrorReason),
            InteractionCommandError.Exception => embed
                .WithTitle("Command Exception")
                .WithDescription(result.ErrorReason),
            InteractionCommandError.Unsuccessful => embed
                .WithTitle("Command Failed")
                .WithDescription(result.ErrorReason),
            _ => embed
                .WithTitle("Something Went Wrong")
                .WithDescription(result.ErrorReason)
        };
    }

    /// <summary>
    /// Responds to an interaction with an error message.
    /// </summary>
    /// <param name="interaction">The interaction to respond to.</param>
    /// <param name="message">The error message to send.</param>
    private async Task RespondWithErrorAsync(SocketInteraction interaction, string message)
    {
        try
        {
            // Autocomplete interactions don't support regular response methods
            if (interaction is SocketAutocompleteInteraction autocomplete)
            {
                // For autocomplete interactions, we respond with empty results to indicate failure
                await autocomplete.RespondAsync(Array.Empty<AutocompleteResult>());
                return;
            }

            if (interaction.HasResponded)
            {
                await interaction.ModifyOriginalResponseAsync(msg => msg.Content = message);
            }
            else
            {
                await interaction.RespondAsync(message, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to respond with error message to interaction {interactionId}", interaction.Id);
        }
    }

    /// <summary>
    /// Gets the command name from a socket interaction.
    /// </summary>
    /// <param name="interaction">The socket interaction.</param>
    /// <returns>The command name or "Unknown" if unable to determine.</returns>
    private static string GetCommandName(SocketInteraction interaction)
    {
        return interaction switch
        {
            SocketSlashCommand slashCommand => GetFullSlashCommandName(slashCommand),
            SocketMessageCommand messageCommand => messageCommand.Data.Name,
            SocketUserCommand userCommand => userCommand.Data.Name,
            SocketAutocompleteInteraction autocompleteInteraction => autocompleteInteraction.Data.CommandName,
            SocketModal modal => $"modal:{modal.Data.CustomId}",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the full slash command name including subcommands and subcommand groups.
    /// </summary>
    /// <param name="slashCommand">The slash command interaction.</param>
    /// <returns>The full command name (e.g., "admin config setup").</returns>
    private static string GetFullSlashCommandName(SocketSlashCommand slashCommand)
    {
        var parts = new List<string> { slashCommand.Data.Name };

        // Add subcommand group if present
        if (slashCommand.Data.Options?.FirstOrDefault()?.Type == ApplicationCommandOptionType.SubCommandGroup)
        {
            var subCommandGroup = slashCommand.Data.Options.First();
            parts.Add(subCommandGroup.Name);

            // Add subcommand within the group
            if (subCommandGroup.Options?.FirstOrDefault()?.Type == ApplicationCommandOptionType.SubCommand)
            {
                parts.Add(subCommandGroup.Options.First().Name);
            }
        }
        // Add subcommand if present (not in a group)
        else if (slashCommand.Data.Options?.FirstOrDefault()?.Type == ApplicationCommandOptionType.SubCommand)
        {
            parts.Add(slashCommand.Data.Options.First().Name);
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Gets the guild ID from a socket interaction.
    /// </summary>
    /// <param name="interaction">The socket interaction.</param>
    /// <returns>The guild ID or null if not in a guild (DM).</returns>
    private static ulong? GetGuildId(SocketInteraction interaction)
    {
        return interaction switch
        {
            SocketSlashCommand slashCommand => slashCommand.GuildId,
            SocketMessageCommand messageCommand => messageCommand.GuildId,
            SocketUserCommand userCommand => userCommand.GuildId,
            SocketAutocompleteInteraction autocompleteInteraction => autocompleteInteraction.GuildId,
            SocketModal modal => modal.GuildId,
            _ => null
        };
    }
}