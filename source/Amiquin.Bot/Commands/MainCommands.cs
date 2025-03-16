using System.Text;
using Amiquin.Core.Discord;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Discord;
using Discord.Interactions;
namespace Amiquin.Bot.Commands;

public class MainCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IChatService _chatService;
    private readonly IMessageCacheService _messageCacheService;

    public MainCommands(IChatService chatService, IMessageCacheService messageCacheService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
    }

    [SlashCommand("chat", "Chat with the bot")]
    public async Task ChatAsync(string message)
    {
        var originalMessage = message;
        var response = await _chatService.ChatAsync(Context.Channel.Id, Context.User.Id, $"{Context.User.GlobalName}: {message}");

        StringBuilder sb = new();
        sb.AppendLine($"{Context.User.Mention}: {originalMessage}");
        sb.AppendLine();
        sb.AppendLine($"{Context.Client.CurrentUser.Mention}: {response}");

        int messageCount = _messageCacheService.GetChatMessageCount(Context.Channel.Id);
        Embed embed = new EmbedBuilder()
            .WithDescription(sb.ToString())
            .WithAuthor(Context.User)
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
            .WithColor(Color.Magenta)
            .WithFooter($"Remembering last {messageCount} messages")
            .Build();

        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }
}