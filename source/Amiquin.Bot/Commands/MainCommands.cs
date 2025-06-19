using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Utilities;
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
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ChatAsync(string message)
    {
        message = message.Trim();
        var originalMessage = message;
        var response = await _chatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, $"[{Context.User.GlobalName}]: {message}");
        int messageCount = _messageCacheService.GetChatMessageCount(Context.Guild.Id);

        // User Embed
        Embed userEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithAuthor(Context.User)
            .WithColor(Color.Teal)
            .Build();

        var botEmbeds = DiscordUtilities.ChunkMessage(response, (chunk, chunkIndex, chunkCount) =>
        {
            return new EmbedBuilder()
                .WithDescription(chunk)
                .WithAuthor(Context.Client.CurrentUser)
                .WithColor(Color.Purple)
                .WithFooter($"☁️ Remembering last {messageCount} messages ☁️ {chunkIndex}/{chunkCount}")
                .Build();
        }).ToList();


        if (botEmbeds.Count == 1)
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new[] { userEmbed, botEmbeds.First() }; });
            return;
        }
        else
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new[] { userEmbed }; });
            foreach (var botEmbed in botEmbeds)
            {
                await Context.Channel.SendMessageAsync(embed: botEmbed);
            }
        }
    }
}