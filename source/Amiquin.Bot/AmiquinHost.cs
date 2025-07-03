using Amiquin.Core;
using Amiquin.Core.Job;
using Amiquin.Core.Options;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Utilities;
using Amiquin.Infrastructure;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reflection;

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
    private readonly ExternalOptions _externalOptions;
    private readonly IJobService _jobService;
    private bool _isInitialized = false;

    public AmiquinHost(IEventHandlerService eventHandlerService, DiscordShardedClient discordClient, InteractionService interactionService, ILogger<AmiquinHost> logger, IOptions<BotOptions> botOptions, ICommandHandlerService commandHandlerService, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration, IOptions<ExternalOptions> externalOptions, IJobService jobService)
    {
        _eventHandlerService = eventHandlerService;
        _client = discordClient;
        _interactionService = interactionService;
        _logger = logger;
        _commandHandlerService = commandHandlerService;
        _botOptions = botOptions.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
        _externalOptions = externalOptions.Value;
        _jobService = jobService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateDatabaseAsync();
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
        _jobService.StartRunnableJobs();

        _isInitialized = true;
        DisplayData();
    }

    private async Task CreateBotAsync()
    {
        var token = _configuration.GetValue<string>(Constants.Environment.BotToken);
        if (string.IsNullOrEmpty(token))
            token = _botOptions.Token;

        _logger.LogInformation("Creating bot");
        _logger.LogInformation("Logging in with token: {Token}", StringModifier.Anomify(token));

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        _logger.LogInformation("Creating database if not exists");
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AmiquinContext>();

        _logger.LogInformation("Applying migrations to database");
        _logger.LogInformation("Using provider: {Provider}", dbContext.Database.ProviderName);
        await dbContext.Database.MigrateAsync();
    }

    private void DisplayData()
    {
        var shouldPrintLogo = _configuration.GetValue<bool>(Constants.Environment.PrintLogo);
        if (shouldPrintLogo)
            Console.Writer.WriteLogo();

        Console.Writer.WriteJsonData("Bot Options", _botOptions);
        Console.Writer.WriteJsonData("External Options", _externalOptions);
        Dictionary<string, string> envVariables = new()
        {
            { Constants.Environment.BotToken, StringModifier.Anomify(_configuration.GetValue<string>(Constants.Environment.BotToken) ?? string.Empty) ?? "null" },
            { Constants.Environment.OpenAiKey, StringModifier.Anomify(_configuration.GetValue<string>(Constants.Environment.OpenAiKey) ?? string.Empty) ?? "null" },
            { Constants.Environment.LogsPath, _configuration.GetValue<string>(Constants.Environment.LogsPath) ?? "null" },
            { Constants.Environment.PrintLogo, _configuration.GetValue<string>(Constants.Environment.PrintLogo) ?? "null" },
            { Constants.Environment.SQLitePath, _configuration.GetValue<string>(Constants.Environment.SQLitePath) ?? "null" },
            { Constants.Environment.TTSModelName, _configuration.GetValue<string>(Constants.Environment.TTSModelName) ?? "null" },
            { Constants.Environment.PiperCommand, _configuration.GetValue<string>(Constants.Environment.PiperCommand) ?? "null" },
            { Constants.Environment.MessageFetchCount, _configuration.GetValue<string>(Constants.Environment.MessageFetchCount) ?? "null" },
        };
        Console.Writer.WriteDictionaryData("Environment Variables", envVariables);

        Dictionary<string, string> calculatedPaths = new()
        {
            { "TTSBasePath", Constants.Paths.TTSBasePath },
            { "TTSBaseOutputPath", Constants.Paths.TTSBaseOutputPath },
            { "MessageBasePath", Constants.Paths.Assets },
        };
        Console.Writer.WriteDictionaryData("Calculated Paths", calculatedPaths);

        var ephemeralCommands = _commandHandlerService.EphemeralCommands;
        var commands = _commandHandlerService.Commands;

        Console.Writer.WriteList("Ephemeral Commands", ephemeralCommands);
        Console.Writer.WriteDictionaryData("Commands", commands.ToDictionary(x => $"/{(string.IsNullOrEmpty(x.Module.SlashGroupName) ? "" : x.Module.SlashGroupName + " ")}{x.Name}", x => x.Description));

        // Get version from assembly
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        var configVersion = _configuration.GetValue<string>(Constants.Environment.BotVersion);
        var displayVersion = !string.IsNullOrEmpty(configVersion) ? configVersion : assemblyVersion;

        Dictionary<string, string> data = new()
        {
            { "ID", _client.CurrentUser.Id.ToString() },
            { "Name Const", _configuration.GetValue<string>(Constants.Environment.BotName) ?? "Amiquin" },
            { "Name", _client.CurrentUser.Username},
            { "Discriminator", _client.CurrentUser.Discriminator},
            { "Global Name", _client.CurrentUser.GlobalName},
            { "Email", _client.CurrentUser.Email},
            { "Created Date", _client.CurrentUser.CreatedAt.ToString("dd-MM-yyyy")},
            { "Version", displayVersion },
            { "Shards", _client.Shards.Count.ToString() },
            { "Guilds", _client.Guilds.Count.ToString() },
            { "Users", _client.Guilds.Sum(x => x.MemberCount).ToString() },
            { "Commands", commands.Count.ToString() },
            { "Ephemeral Commands", ephemeralCommands.Count.ToString() },
            { "IsInitialized", _isInitialized.ToString() },
            { "Avatar Url", _client.CurrentUser.GetDisplayAvatarUrl()},
            { "IsBot", _client.CurrentUser.IsBot ? "Yes" : "No"},
            { "IsWebhook", _client.CurrentUser.IsWebhook ? "Yes" : "No"},
            { "Login state", _client.LoginState.ToString() },
        };

        Console.Writer.WriteDictionaryData("Bot Data", data);
    }
}