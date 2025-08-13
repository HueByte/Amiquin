using Amiquin.Core.Options;
using Amiquin.Core.Services.MessageCache;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.ServerInteraction;

/// <summary>
/// Implementation of the <see cref="IServerInteractionService"/> interface.
/// Handles interactions with Discord servers.
/// </summary>
public class ServerInteractionService : IServerInteractionService
{
    private readonly IMessageCacheService _messageCacheService;
    private readonly ILogger<ServerInteractionService> _logger;
    private readonly BotOptions _botOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerInteractionService"/> class.
    /// </summary>
    /// <param name="messageCacheService">The service used to retrieve cached messages.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="botOptions">The bot configuration options.</param>
    public ServerInteractionService(IMessageCacheService messageCacheService, ILogger<ServerInteractionService> logger, IOptions<BotOptions> botOptions)
    {
        _messageCacheService = messageCacheService;
        _logger = logger;
        _botOptions = botOptions.Value;
    }

    /// <inheritdoc/>
    public async Task SendJoinMessageAsync(SocketGuild guild)
    {
        var botName = _botOptions.Name;
        var joinMessage = await _messageCacheService.GetServerJoinMessage();
        var components = new ComponentBuilderV2()
            .WithTextDisplay($"# ✨ Hello I'm {botName}! ✨")
            .WithTextDisplay(joinMessage)
            .Build();

        _logger.LogInformation("Sending join message to {GuildName} in default channel {channel}", guild.Name, guild.DefaultChannel.Name);
        await guild.DefaultChannel.SendMessageAsync(components: components, flags: MessageFlags.ComponentsV2);
    }
}