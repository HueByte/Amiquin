using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Bot;

public class AmiquinHost : IHostedService
{
    private readonly ILogger<AmiquinHost> _logger;
    private readonly DiscordShardedClient _client;
    private readonly IEventHandlerService _eventHandlerService;
    private readonly InteractionService _interactionService;
    private readonly ICommandHandlerService _commandHandlerService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private readonly BotOptions _botOptions;
    private bool _isInitialized = false;

    public AmiquinHost(IEventHandlerService eventHandlerService, DiscordShardedClient discordClient, InteractionService interactionService, ILogger<AmiquinHost> logger, IOptions<BotOptions> botOptions, ICommandHandlerService commandHandlerService, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
    {
        _eventHandlerService = eventHandlerService;
        _client = discordClient;
        _interactionService = interactionService;
        _logger = logger;
        _commandHandlerService = commandHandlerService;
        _botOptions = botOptions.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _commandHandlerService.InitializeAsync();

        AttachEvents();
        await CreateBotAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Amiquin Bot");
        await _client.StopAsync();
    }

    private void AttachEvents()
    {
        _client.ShardReady += OnShardReadyAsync;
        _client.ShardReady += _eventHandlerService.OnShardReadyAsync;
        _client.InteractionCreated += _eventHandlerService.OnCommandCreatedAsync;
        _client.Log += _eventHandlerService.OnClientLogAsync;
        _client.JoinedGuild += _eventHandlerService.OnBotJoinedAsync;
        _interactionService.SlashCommandExecuted += _eventHandlerService.OnShashCommandExecutedAsync;
    }

    private async Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        if (_isInitialized) return;

        _logger.LogInformation("Registering slash commands");
        await _interactionService.RegisterCommandsGloballyAsync();

        _isInitialized = true;
        DisplayData();
    }

    private async Task CreateBotAsync()
    {
        var token = _configuration.GetValue<string>(Constants.BotToken);
        if (string.IsNullOrEmpty(token))
            token = _botOptions.Token;

        _logger.LogInformation("Creating bot");
        _logger.LogInformation("Logging in with token: {Token}", StringModifier.Anomify(token));

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    private void DisplayData()
    {
        var shouldPrintLogo = _configuration.GetValue<bool>(Constants.PrintLogo);
        if (shouldPrintLogo)
            Console.Writer.WriteLogo();

        Console.Writer.WriteJsonData("Bot Options", _botOptions);

        var ephemeralCommands = _commandHandlerService.EphemeralCommands;
        var commands = _commandHandlerService.Commands;

        Console.Writer.WriteList("Ephemeral Commands", ephemeralCommands);
        Console.Writer.WriteDictionaryData("Commands", commands.ToDictionary(x => $"/{(string.IsNullOrEmpty(x.Module.SlashGroupName) ? "" : x.Module.SlashGroupName + " ")}{x.Name}", x => x.Description));
    }
}