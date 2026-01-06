using Amiquin.Bot.Commands;
using Amiquin.Core;
using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.Configuration;
using Amiquin.Core.Services.Embeddings;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Utilities;
using Amiquin.Infrastructure;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
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
    private readonly BotOptions _botOptions;
    private readonly DiscordOptions _discordOptions;
    private readonly VoiceOptions _voiceOptions;
    private readonly DataPathOptions _dataPathOptions;
    private readonly MemoryOptions _memoryOptions;
    private readonly IJobService _jobService;
    private readonly IQdrantMemoryRepository _qdrantMemoryRepository;
    private bool _isInitialized = false;

    public AmiquinHost(
        IEventHandlerService eventHandlerService,
        DiscordShardedClient discordClient,
        InteractionService interactionService,
        ILogger<AmiquinHost> logger,
        IOptions<BotOptions> botOptions,
        ICommandHandlerService commandHandlerService,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<DiscordOptions> discordOptions,
        IOptions<VoiceOptions> voiceOptions,
        IOptions<DataPathOptions> dataPathOptions,
        IOptions<MemoryOptions> memoryOptions,
        IJobService jobService,
        IQdrantMemoryRepository qdrantMemoryRepository)
    {
        _eventHandlerService = eventHandlerService;
        _client = discordClient;
        _interactionService = interactionService;
        _logger = logger;
        _commandHandlerService = commandHandlerService;
        _botOptions = botOptions.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _discordOptions = discordOptions.Value;
        _voiceOptions = voiceOptions.Value;
        _dataPathOptions = dataPathOptions.Value;
        _memoryOptions = memoryOptions.Value;
        _jobService = jobService;
        _qdrantMemoryRepository = qdrantMemoryRepository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateDatabaseAsync();
        await InitializeQdrantAsync();
        await ValidateEmbeddingProviderAsync();
        await _commandHandlerService.InitializeAsync();

        // Initialize configuration interaction handlers with scoped service
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationInteractionService>();
            configurationService.Initialize();

            // Initialize session component handlers (scoped service)
            var sessionComponentHandlers = scope.ServiceProvider.GetRequiredService<SessionComponentHandlers>();
            // Handlers are registered in constructor, so just getting the service initializes them

            // Initialize NSFW component handlers (scoped service)
            var nsfwComponentHandlers = scope.ServiceProvider.GetRequiredService<NsfwComponentHandlers>();
            // Handlers are registered in constructor, so just getting the service initializes them
        }

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
        _client.MessageReceived += _eventHandlerService.OnMessageReceivedAsync;
        _interactionService.SlashCommandExecuted += _eventHandlerService.OnSlashCommandExecutedAsync;

        _client.Log += _eventHandlerService.OnClientLogAsync;
        _client.JoinedGuild += _eventHandlerService.OnBotJoinedAsync;
        _client.UserJoined += _eventHandlerService.OnUserJoinedAsync;
    }

    private async Task OnShardReadyAsync(DiscordSocketClient shard)
    {
        if (_isInitialized) return;

        _logger.LogInformation("Registering slash commands");
        await _interactionService.RegisterCommandsGloballyAsync();
        _jobService.StartRunnableJobs();

        // Initialize activity context for all guilds
        await InitializeActivityContextAsync();

        _isInitialized = true;
        DisplayData();
    }

    private async Task CreateBotAsync()
    {
        var token = _discordOptions.Token;

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

    private async Task InitializeQdrantAsync()
    {
        if (!_memoryOptions.Enabled)
        {
            _logger.LogInformation("Memory system is disabled, skipping Qdrant initialization");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing Qdrant vector database collection");
            var result = await _qdrantMemoryRepository.InitializeCollectionAsync();

            if (result)
            {
                _logger.LogInformation("Qdrant collection initialized successfully");

                // Log collection info
                var isHealthy = await _qdrantMemoryRepository.IsHealthyAsync();
                _logger.LogInformation("Qdrant health status: {Status}", isHealthy ? "Healthy" : "Unhealthy");
            }
            else
            {
                _logger.LogWarning("Qdrant collection initialization returned false - collection may not be available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Qdrant collection. Memory features may not work correctly.");
        }
    }

    private async Task ValidateEmbeddingProviderAsync()
    {
        if (!_memoryOptions.Enabled)
        {
            _logger.LogInformation("Memory system is disabled, skipping embedding provider validation");
            return;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var embeddingOptions = scope.ServiceProvider.GetRequiredService<IOptions<EmbeddingOptions>>().Value;

        _logger.LogInformation("Validating embedding provider '{ProviderId}' (configured: {ConfiguredProvider})",
            embeddingProvider.ProviderId, embeddingOptions.Provider);

        var isAvailable = await embeddingProvider.IsAvailableAsync();

        if (!isAvailable)
        {
            var errorMessage = $"Embedding provider '{embeddingProvider.ProviderId}' is not available. " +
                              $"Memory features require a working embedding provider. " +
                              $"Please check your configuration and ensure the provider is running.";

            _logger.LogCritical(errorMessage);

            if (embeddingOptions.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Ollama provider is configured but not available. " +
                               "Ensure Ollama is running at {BaseUrl} and the model '{Model}' is pulled.",
                    embeddingOptions.Ollama.BaseUrl, embeddingOptions.Ollama.Model);
            }
            else if (embeddingOptions.Provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("OpenAI provider is configured but not available. " +
                               "Ensure your OpenAI API key is valid and configured correctly.");
            }

            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogInformation("Embedding provider '{ProviderId}' validated successfully (dimension: {Dimension})",
            embeddingProvider.ProviderId, embeddingProvider.EmbeddingDimension);
    }

    private void DisplayData()
    {
        if (_botOptions.PrintLogo)
            Console.Writer.WriteLogo();

        Console.Writer.WriteJsonData("Bot Options", _botOptions);
        Console.Writer.WriteJsonData("Discord Options", _discordOptions);
        Console.Writer.WriteJsonData("Voice Options", _voiceOptions);
        Console.Writer.WriteJsonData("Data Path Options", _dataPathOptions);

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
        var displayVersion = !string.IsNullOrEmpty(_botOptions.Version) ? _botOptions.Version : assemblyVersion;

        Dictionary<string, string> data = new()
        {
            { "ID", _client.CurrentUser.Id.ToString() },
            { "Name Const", _botOptions.Name },
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

    private async Task InitializeActivityContextAsync()
    {
        try
        {
            _logger.LogInformation("Initializing activity context for all guilds");

            using var scope = _serviceScopeFactory.CreateScope();
            var chatContextService = scope.ServiceProvider.GetRequiredService<IChatContextService>();

            var guilds = _client.Guilds.ToArray();
            _logger.LogDebug("Found {Count} guilds to initialize", guilds.Length);

            var initializationTasks = new List<Task>();
            foreach (var guild in guilds)
            {
                // Run initializations concurrently but limit to avoid rate limits
                initializationTasks.Add(chatContextService.InitializeActivityContextAsync(guild));

                // Process in batches of 3 to avoid overwhelming Discord API
                if (initializationTasks.Count >= 3)
                {
                    await Task.WhenAll(initializationTasks);
                    initializationTasks.Clear();

                    // Small delay between batches to be respectful to Discord API
                    await Task.Delay(500);
                }
            }

            // Process any remaining tasks
            if (initializationTasks.Count > 0)
            {
                await Task.WhenAll(initializationTasks);
            }

            _logger.LogInformation("Activity context initialization completed for {Count} guilds", guilds.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during activity context initialization");
        }
    }
}