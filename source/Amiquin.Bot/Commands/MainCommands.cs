using System.Text;
using Amiquin.Core.Discord;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Discord;
using Discord.Interactions;
namespace Amiquin.Bot.Commands;

public class MainCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IPersonaChatService _chatService;
    private readonly IMessageCacheService _messageCacheService;

    public MainCommands(IPersonaChatService chatService, IMessageCacheService messageCacheService)
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

        // User Embed
        Embed userEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithAuthor(Context.User)
            .WithColor(Color.Teal)
            .Build();

        // Bot Embed
        Embed botEmbed = new EmbedBuilder()
            .WithDescription(response)
            .WithAuthor(Context.Client.CurrentUser)
            .WithColor(Color.Purple)
            .WithFooter($"Remembering last {messageCount} messages")
            .Build();

        await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new[] { userEmbed, botEmbed }; });
    }
}