using Amiquin.Core;
using Amiquin.Core.Abstraction;
using Amiquin.Core.Exceptions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Job;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.BotSession;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.ExternalProcessRunner;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Nacho;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.ServerInteraction;
using Amiquin.Core.Services.Voice;
using Amiquin.Infrastructure;
using Amiquin.Infrastructure.Repositories;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Serilog;
using Jiro.Shared.Tasks;

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
            LogLevel = LogSeverity.Info,
            MessageCacheSize = 100,
            GatewayIntents = GatewayIntents.Guilds
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
                 .AddSingleton(interactionService)
                 .AddSingleton<BotSessionService>()
                 .AddSingleton<ICommandHandlerService, CommandHandlerService>()
                 .AddSingleton<IEventHandlerService, EventHandlerService>()
                 .AddSingleton<IChatSemaphoreManager, ChatSemaphoreManager>()
                 .AddSingleton<IVoiceStateManager, VoiceStateManager>()
                 .AddSingleton<IJobService, JobService>()
                 .AddSingleton<ITaskManager, TaskManager>()
                 .AddMemoryCache();

        var dbOptions = _configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var databaseMode = _configuration.GetValue<string>(Constants.Environment.DatabaseMode)
            ?? dbOptions?.Mode.ToString() ?? "0";

        var parsedDatabaseMode = GetDatabaseModeValue(databaseMode);
        if (parsedDatabaseMode.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
        {
            _services.AddAmiquinContext(_configuration);
        }
        else if (parsedDatabaseMode.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        {
            _services.AddAmiquinMySqlContext(_configuration);
        }
        else if (parsedDatabaseMode.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Postgres database mode is selected, but implementation is not yet available.");
            throw new DatabaseNotImplementedException("Postgres database mode is not yet implemented.");

            // TODO: Implement Postgres context setup
            // _services.AddAmiquinPostgresContext(_configuration);
        }
        else if (parsedDatabaseMode.Equals("mssql", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("MSSQL database mode is selected, but implementation is not yet available.");
            throw new DatabaseNotImplementedException("MSSQL database mode is not yet implemented.");

            // TODO: Implement MSSQL context setup
            // _services.AddAmiquinMssqlContext(_configuration);
        }
        else
        {
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
                 .AddScoped<IChatCoreService, ChatCoreService>()
                 .AddScoped<IPersonaService, PersonaService>()
                 .AddScoped<IPersonaChatService, PersonaChatService>()
                 .AddScoped<IVoiceService, VoiceService>()
                 .AddScoped<INewsApiClient, NewsApiClient>()
                 .AddScoped<IHistoryOptimizerService, HistoryOptimizerService>()
                 .AddScoped<IToggleService, ToggleService>()
                 .AddScoped<BotContextAccessor>()
                 .AddScoped<IServerMetaService, ServerMetaService>()
                 .AddScoped<INachoService, NachoService>();

        // LLM-based chat services (new system)
        _services.AddScoped<LLMChatService>()
                 .AddScoped<IMultiProviderChatService>(provider => provider.GetRequiredService<LLMChatService>());

        // Discord bot chat services
        _services.AddSingleton<ISemaphoreManager, SemaphoreManager>()
                 .AddScoped<IMessageManager, MessageManager>();

        _services.AddTransient<IExternalProcessRunnerService, ExternalProcessRunnerService>();

        _services.AddScoped<ChatClient>((services) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var openAIProvider = llmOptions.GetProvider("OpenAI");
            string openApiKey = openAIProvider?.ApiKey ?? throw new InvalidOperationException("OpenAI API key is not configured");

            return new ChatClient(Constants.AI.Gpt4oMiniModel, openApiKey);
        });

        _services.AddHttpClient(typeof(INewsApiClient).Name, (services, client) =>
        {
            var externalUrls = services.GetRequiredService<IOptions<ExternalOptions>>().Value;
            client.BaseAddress = new Uri(externalUrls.NewsApiUrl);
        });

        // Configure HTTP clients for LLM providers
        _services.AddHttpClient("LLM_OpenAI", (services, client) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var openAIConfig = llmOptions.GetProvider("OpenAI");
            if (openAIConfig != null && openAIConfig.Enabled)
            {
                client.BaseAddress = new Uri(openAIConfig.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(llmOptions.GlobalTimeout);
            }
        });

        _services.AddHttpClient("LLM_Grok", (services, client) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var grokConfig = llmOptions.GetProvider("Grok");
            if (grokConfig != null && grokConfig.Enabled)
            {
                client.BaseAddress = new Uri(grokConfig.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(llmOptions.GlobalTimeout);
            }
        });

        _services.AddHttpClient("LLM_Gemini", (services, client) =>
        {
            var llmOptions = services.GetRequiredService<IOptions<LLMOptions>>().Value;
            var geminiConfig = llmOptions.GetProvider("Gemini");
            if (geminiConfig != null && geminiConfig.Enabled)
            {
                client.BaseAddress = new Uri(geminiConfig.BaseUrl);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(llmOptions.GlobalTimeout);
            }
        });

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
                 .AddScoped<IToggleRepository, ToggleRepository>()
                 .AddScoped<IServerMetaRepository, ServerMetaRepository>()
                 .AddScoped<INachoRepository, NachoRepository>()
                 .AddScoped<ICommandLogRepository, CommandLogRepository>()
                 .AddScoped<IBotStatisticsRepository, BotStatisticsRepository>();

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

        return this;
    }
}