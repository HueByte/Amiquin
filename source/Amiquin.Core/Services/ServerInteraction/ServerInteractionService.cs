using Amiquin.Core.Services.MessageCache;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ServerInteraction;

public class ServerInteractionService : IServerInteractionService
{
    private readonly IMessageCacheService _messageCacheService;
    private readonly ILogger<ServerInteractionService> _logger;
    public ServerInteractionService(IMessageCacheService messageCacheService, ILogger<ServerInteractionService> logger)
    {
        _messageCacheService = messageCacheService;
        _logger = logger;
    }

    public async Task SendJoinMessageAsync(SocketGuild guild)
    {
        var joinMessage = await _messageCacheService.GetServerJoinMessage();
        var embed = new EmbedBuilder().WithTitle("✨ Hello I'm Amiquin! ✨")
                                     .WithColor(Color.Teal)
                                     .WithDescription(joinMessage)
                                     .WithCurrentTimestamp();

        _logger.LogInformation("Sending join message to {GuildName} in default channel {channel}", guild.Name, guild.DefaultChannel.Name);
        await guild.DefaultChannel.SendMessageAsync(null, false, embed.Build());
    }
}