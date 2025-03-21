using Amiquin.Core;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.ExternalProcessRunner;
using Amiquin.Core.Services.MessageCache;
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
                 .AddSingleton<ICommandHandlerService, CommandHandlerService>()
                 .AddSingleton<IEventHandlerService, EventHandlerService>()
                 .AddSingleton<IChatSemaphoreManager, ChatSemaphoreManager>()
                 .AddSingleton<IVoiceStateManager, VoiceStateManager>()
                 .AddAmiquinContext(_configuration)
                 .AddMemoryCache();

        return this;
    }

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
                 .AddScoped<IToggleService, ToggleService>();

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

    public InjectionConfigurator AddRepositories()
    {
        _services.AddScoped<IMessageRepository, MessageRepository>()
                 .AddScoped<IToggleRepository, ToggleRepository>();

        return this;
    }

    public InjectionConfigurator AddOptions()
    {
        _services.AddOptions<BotOptions>().Bind(_configuration.GetSection(BotOptions.Bot));
        _services.AddOptions<ExternalOptions>().Bind(_configuration.GetSection(ExternalOptions.External));

        return this;
    }
}