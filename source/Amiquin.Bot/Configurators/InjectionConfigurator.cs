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
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.ExternalProcessRunner;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Nacho;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.ServerInteraction;
using Amiquin.Core.Services.ServerMeta;
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
        BotSessionService botSessionService = new(_configuration);

        _services.AddHostedService<AmiquinHost>()
                 .AddSingleton(client)
                 .AddSingleton(interactionService)
                 .AddSingleton(botSessionService)
                 .AddSingleton<ICommandHandlerService, CommandHandlerService>()
                 .AddSingleton<IEventHandlerService, EventHandlerService>()
                 .AddSingleton<IChatSemaphoreManager, ChatSemaphoreManager>()
                 .AddSingleton<IVoiceStateManager, VoiceStateManager>()
                 .AddSingleton<IJobService, JobService>()
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
        "0" => "sqlite",
        "1" => "mysql",
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

        _services.AddTransient<IExternalProcessRunnerService, ExternalProcessRunnerService>();

        _services.AddScoped<ChatClient>((services) =>
        {
            var configManager = services.GetRequiredService<IConfiguration>();
            string openApiKey = configManager.GetValue<string>(Constants.Environment.OpenAiKey)
                ?? services.GetRequiredService<IOptions<BotOptions>>().Value.OpenAIKey;

            return new ChatClient(Constants.AI.Gpt4oMiniModel, openApiKey);
        });

        _services.AddHttpClient(typeof(INewsApiClient).Name, (services, client) =>
        {
            var externalUrls = services.GetRequiredService<IOptions<ExternalOptions>>().Value;
            client.BaseAddress = new Uri(externalUrls.NewsApiUrl);
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
        _services.AddOptions<BotOptions>().Bind(_configuration.GetSection(BotOptions.Bot));
        _services.AddOptions<ExternalOptions>().Bind(_configuration.GetSection(ExternalOptions.External));
        _services.Configure<DatabaseOptions>(_configuration.GetSection(DatabaseOptions.Database));

        return this;
    }
}