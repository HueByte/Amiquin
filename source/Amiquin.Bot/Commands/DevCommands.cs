using Amiquin.Core.Attributes;
using Amiquin.Core.Discord;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Discord.Interactions;

namespace Amiquin.Bot.Commands;

[Group("dev", "Developer commands")]
public class DevCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IChatService _chatService;
    private readonly IMessageCacheService _messageCacheService;

    public DevCommands(IChatService chatService, IMessageCacheService messageCacheService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
    }

    [SlashCommand("ping-ephemeral", "Pong! (Ephemeral)")]
    [Ephemeral]
    public async Task PingEphemeralAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Pong!");
    }

    [SlashCommand("ping", "Pong!")]
    public async Task PingAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Pong!");
    }
}