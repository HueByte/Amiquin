using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.CommandHandler;
using Amiquin.Core.Services.EventHandler;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.ServerInteraction;
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
                 .AddMemoryCache();

        return this;
    }

    public InjectionConfigurator AddServices()
    {
        _services.AddScoped<IMessageCacheService, MessageCacheService>()
                 .AddScoped<IServerInteractionService, ServerInteractionService>()
                 .AddScoped<IChatService, ChatService>();

        _services.AddTransient<ChatClient>((services) =>
        {
            var configManager = services.GetRequiredService<IConfiguration>();
            string openApiKey = configManager.GetValue<string>(Constants.OpenApiKey)
                ?? services.GetRequiredService<IOptions<BotOptions>>().Value.OpenAIKey;

            return new ChatClient(Constants.Gpt4oMiniModel, openApiKey);
        });

        return this;
    }

    public InjectionConfigurator AddOptions()
    {
        _services.AddOptions<BotOptions>().Bind(_configuration.GetSection(BotOptions.BOT));

        return this;
    }

    public InjectionConfigurator AddDatabaseServices()
    {
        var databaseConnString = _configuration.GetConnectionString("AmiquinContext");
        // _services.AddAmiquinDatabase(databaseConnString ?? "");

        return this;
    }
}