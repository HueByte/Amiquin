using Amiquin.Bot.Commands;
using Amiquin.Core;
using Amiquin.Core.Abstraction;
using Amiquin.Core.Abstractions;
using Amiquin.Core.Configuration;
using Amiquin.Core.Exceptions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ActivitySession;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.BotSession;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Configuration;
using Amiquin.Core.Services.ErrorHandling;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.ExternalProcessRunner;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Memory;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Modal;
using Amiquin.Core.Services.ModelProvider;
using Amiquin.Core.Services.Nacho;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Nsfw.Providers;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.Scrappers;
using Amiquin.Core.Services.ServerInteraction;
using Amiquin.Core.Services.SessionManager;
using Amiquin.Core.Services.Sleep;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Services.Voice;
using Amiquin.Infrastructure;
using Amiquin.Infrastructure.Repositories;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Jiro.Shared.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Serilog;

namespace Amiquin.Bot.Configurators;

public class InjectionConfigurator
{
    private readonly IConfiguration _configuration;
    private readonly IServiceCollection _services;

    public InjectionConfigurator(IConfiguration configuration, IServiceCollection services)
    {
        _configuration = configuration;
        _services = services;
    }

    public InjectionConfigurator AddAmiquinCore()
    {
        DiscordShardedClient client = new(new DiscordSocketConfig
        {
            UseInteractionSnowflakeDate = false,
            LogLevel = LogSeverity.Info,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.Guilds
                | GatewayIntents.GuildMessages
                | GatewayIntents.MessageContent
                | GatewayIntents.GuildBans
                | GatewayIntents.GuildEmojis
                | GatewayIntents.GuildIntegrations
                | GatewayIntents.GuildWebhooks
                | GatewayIntents.GuildInvites
                | GatewayIntents.GuildVoiceStates
                | GatewayIntents.GuildMessageReactions
                | GatewayIntents.DirectMessageReactions
                | GatewayIntents.GuildScheduledEvents
                | GatewayIntents.GuildMembers
                | GatewayIntents.GuildVoiceStates
        });

        InteractionServiceConfig interactionServiceConfig = new()
        {
            AutoServiceScopes = false,
            DefaultRunMode = RunMode.Async,
        };

        InteractionService interactionService = new(client, interactionServiceConfig);

        _services.AddHostedService<AmiquinHost>()
                 .AddSingleton(client)
                 .AddSingleton<IDiscordClientWrapper>(provider => new DiscordClientWrapper(provider.GetRequiredService<DiscordShardedClient>()))
                 .AddSingleton(interactionService)
                 .AddSingleton<IChatContextService, ChatContextService>()
                 .AddSingleton<BotSessionService>()
                 .AddSingleton<ICommandHandlerService, CommandHandlerService>()
                 .AddSingleton<IEventHandlerService, EventHandlerService>()
                 .AddSingleton<IChatSemaphoreManager, ChatSemaphoreManager>()
                 .AddSingleton<IVoiceStateManager, VoiceStateManager>()
                 .AddSingleton<IJobService, JobService>()
                 .AddSingleton<ITaskManager, TaskManager>()
                 .AddSingleton<IComponentHandlerService, ComponentHandlerService>()
                 .AddSingleton<IModalService, ModalService>()
                 .AddSingleton<IInteractionErrorHandlerService, InteractionErrorHandlerService>()
                 .AddScoped<IConfigurationInteractionService, ConfigurationInteractionService>()
                 .AddSingleton<IPaginationService, PaginationService>()
                 .AddMemoryCache();

        var dbOptions = _configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var databaseMode = _configuration.GetValue<string>(Constants.Environment.DatabaseMode)
            ?? dbOptions?.Mode.ToString() ?? "0";

        var parsedDatabaseMode = GetDatabaseModeValue(databaseMode);
        switch (parsedDatabaseMode.ToLowerInvariant())
        {
            case "sqlite":
                _services.AddAmiquinContext(_configuration);
                break;
                
            case "mysql":
                _services.AddAmiquinMySqlContext(_configuration);
                break;
                
            case "postgres":
                Log.Warning("Postgres database mode is selected, but implementation is not yet available.");
                throw new DatabaseNotImplementedException("Postgres database mode is not yet implemented.");
                
                // TODO: Implement Postgres context setup
                // _services.AddAmiquinPostgresContext(_configuration);
                
            case "mssql":
                Log.Warning("MSSQL database mode is selected, but implementation is not yet available.");
                throw new DatabaseNotImplementedException("MSSQL database mode is not yet implemented.");
                
                // TODO: Implement MSSQL context setup
                // _services.AddAmiquinMssqlContext(_configuration);
                
            default:
                throw new InvalidOperationException($"Unsupported database mode: {databaseMode}");
        }

        return this;
    }

    private string GetDatabaseModeValue(string? value) => value switch
    {
        null or "" => "sqlite",
        "0" => "mysql",
        "1" => "sqlite",
        "2" => "postgres",
        "3" => "mssql",
        _ => throw new InvalidOperationException($"Unsupported database mode: {value}")
    };


    public InjectionConfigurator AddServices()
    {
        _services.AddScoped<IMessageCacheService, MessageCacheService>()
                 .AddScoped<IServerInteractionService, ServerInteractionService>()
                 .AddScoped<IChatCoreService, CoreChatService>()
                 .AddScoped<IPersonaService, PersonaService>()
                 .AddScoped<IPersonaChatService, PersonaChatService>()
                 .AddScoped<IFunService, FunService>()
                 .AddScoped<IVoiceService, VoiceService>()
                 .AddScoped<INewsApiClient, NewsApiClient>()
                 .AddScoped<INsfwProvider, WaifuProvider>()
                 .AddScoped<INsfwApiService, NsfwApiService>()
                 .AddScoped<IToggleService, ToggleService>()
                 .AddScoped<BotContextAccessor>()
                 .AddSingleton<IServerMetaService, ServerMetaService>()
                 .AddScoped<INachoService, NachoService>()
                 .AddScoped<IChatSessionService, ChatSessionService>()
                 .AddScoped<IActivitySessionService, ActivitySessionService>()
                 .AddScoped<ISessionManagerService, SessionManagerService>()
                 .AddSingleton<ISleepService, SleepService>()
                 .AddSingleton<IModelProviderMappingService, ModelProviderMappingService>()
                 .AddScoped<IMemoryService, QdrantMemoryService>()
                 .AddScoped<SessionComponentHandlers>()
                 .AddScoped<NsfwComponentHandlers>();

        // Provider factory and providers for managing LLM providers
        _services.AddScoped<IChatProviderFactory, ChatProviderFactory>()
                 .AddScoped<OpenAILLMProvider>(services =>
                 {
                     var logger = services.GetRequiredService<ILogger<OpenAILLMProvider>>();
                     var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
                     var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
                     var providerConfig = llmOptions.GetProvider("OpenAI") ?? new LLMProviderOptions();
                     return new OpenAILLMProvider(logger, httpClientFactory, providerConfig, llmOptions);
                 })
                 .AddScoped<GeminiLLMProvider>(services =>
                 {
                     var logger = services.GetRequiredService<ILogger<GeminiLLMProvider>>();
                     var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
                     var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
                     var providerConfig = llmOptions.GetProvider("Gemini") ?? new LLMProviderOptions();
                     return new GeminiLLMProvider(logger, httpClientFactory, providerConfig, llmOptions);
                 })
                 .AddScoped<GrokLLMProvider>(services =>
                 {
                     var logger = services.GetRequiredService<ILogger<GrokLLMProvider>>();
                     var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
                     var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
                     var providerConfig = llmOptions.GetProvider("Grok") ?? new LLMProviderOptions();
                     return new GrokLLMProvider(logger, httpClientFactory, providerConfig, llmOptions);
                 });

        // Discord bot chat services
        _services.AddSingleton<ISemaphoreManager, SemaphoreManager>();

        _services.AddTransient<IExternalProcessRunnerService, ExternalProcessRunnerService>();

        _services.AddScoped<ChatClient>((services) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var openAIProvider = llmOptions.GetProvider("OpenAI");
            string openApiKey = openAIProvider?.ApiKey ?? throw new InvalidOperationException("OpenAI API key is not configured");

            // TODO: Use model from configuration
            //     string model = llmOptions.GetModel("OpenAI") ?? Constants.AI.Gpt4oMiniModel;
            //     return new ChatClient(model, openApiKey);
            return new ChatClient(Constants.AI.Gpt4oMiniModel, openApiKey);
        });

        _services.AddScoped<OpenAIClient>((services) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var openAIProvider = llmOptions.GetProvider("OpenAI");
            if (openAIProvider?.ApiKey == null)
            {
                // Return null if no API key - memory service will handle gracefully
                return null!;
            }
            return new OpenAIClient(openAIProvider.ApiKey);
        });

        _services.AddHttpClient(typeof(INewsApiClient).Name, (services, client) =>
        {
            var externalUrls = services.GetRequiredService<IOptions<ExternalOptions>>().Value;
            client.BaseAddress = new Uri(externalUrls.NewsApiUrl);
        });

        _services.AddHttpClient<WaifuProvider>((services, client) =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register scrapper manager service
        _services.AddSingleton<IScrapperManagerService, ScrapperManagerService>();
        
        // Configure HTTP clients for scrapper providers - the manager will create specific clients per provider
        _services.AddHttpClient("Scrapper_Default", (services, client) =>
        {
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Configure HTTP clients for LLM providers dynamically
        var llmOptions = _configuration.GetSection(LLMOptions.SectionName).Get<LLMOptions>() ?? new LLMOptions();

        foreach (var (providerName, providerConfig) in llmOptions.Providers)
        {
            _services.AddHttpClient($"LLM_{providerName}", (services, client) =>
            {
                var currentLlmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
                var currentProviderConfig = currentLlmOptions.GetProvider(providerName);
                
                if (currentProviderConfig != null && currentProviderConfig.Enabled && !string.IsNullOrEmpty(currentProviderConfig.BaseUrl))
                {
                    client.BaseAddress = new Uri(currentProviderConfig.BaseUrl);
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.Timeout = TimeSpan.FromSeconds(currentLlmOptions.GlobalTimeout);
                }
            });
        }

        return this;
    }

    public InjectionConfigurator AddRunnableJobs()
    {
        var jobTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => typeof(IRunnableJob).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var jobType in jobTypes)
        {
            _services.AddScoped(typeof(IRunnableJob), jobType);
        }

        return this;
    }

    public InjectionConfigurator AddRepositories()
    {
        _services.AddScoped<IMessageRepository, MessageRepository>()
                 .AddScoped<ISessionMessageRepository, SessionMessageRepository>()
                 .AddScoped<IToggleRepository, ToggleRepository>()
                 .AddScoped<IServerMetaRepository, ServerMetaRepository>()
                 .AddScoped<INachoRepository, NachoRepository>()
                 .AddScoped<ICommandLogRepository, CommandLogRepository>()
                 .AddScoped<IBotStatisticsRepository, BotStatisticsRepository>()
                 .AddScoped<IChatSessionRepository, ChatSessionRepository>()
                 .AddScoped<IQdrantMemoryRepository, QdrantMemoryRepository>()
                 .AddScoped<IPaginationSessionRepository, PaginationSessionRepository>()
                 .AddScoped<IUserStatsRepository, UserStatsRepository>();

        return this;
    }

    public InjectionConfigurator AddOptions()
    {
        // Main options
        _services.Configure<BotOptions>(_configuration.GetSection(BotOptions.Bot));
        _services.Configure<ExternalOptions>(_configuration.GetSection(ExternalOptions.External));
        _services.Configure<DatabaseOptions>(_configuration.GetSection(DatabaseOptions.Database));

        // Configuration options
        _services.Configure<ChatOptions>(_configuration.GetSection(ChatOptions.SectionName));
        _services.Configure<DataPathOptions>(_configuration.GetSection(DataPathOptions.SectionName));
        _services.Configure<DiscordOptions>(_configuration.GetSection(DiscordOptions.SectionName));
        _services.Configure<VoiceOptions>(_configuration.GetSection(VoiceOptions.SectionName));
        _services.Configure<JobManagerOptions>(_configuration.GetSection(JobManagerOptions.SectionName));

        // TaskManager options (map from JobManager section for now)
        _services.Configure<TaskManagerOptions>(_configuration.GetSection(JobManagerOptions.SectionName));

        // LLM configuration system
        _services.Configure<LLMOptions>(_configuration.GetSection(LLMOptions.SectionName));

        // Scrapper configuration system
        _services.Configure<ScrapperOptions>(_configuration.GetSection(ScrapperOptions.SectionName));

        // Memory configuration system
        _services.Configure<MemoryOptions>(_configuration.GetSection(MemoryOptions.Section));

        return this;
    }
}