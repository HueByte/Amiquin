using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using System.Reflection;
namespace Amiquin.Bot.Commands;

public class MainCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IPersonaChatService _chatService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatContextService _chatContextService;

    public MainCommands(IPersonaChatService chatService, IMessageCacheService messageCacheService, IChatContextService chatContextService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _chatContextService = chatContextService;
    }

    [SlashCommand("chat", "Chat with amiquin!")]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ChatAsync(string message)
    {
        message = message.Trim();
        var originalMessage = message;

        // Get context from the ChatContextService
        var context = _chatContextService.FormatContextMessagesForAI(Context.Guild.Id);
        var username = Context.User.GlobalName ?? Context.User.Username;
        var userId = Context.User.Id;

        // Build the prompt with context if available
        var contextPrompt = !string.IsNullOrWhiteSpace(context) ? $"\nRecent context:\n{context}" : "";
        var prompt = $"{contextPrompt}\n[{username}:{userId}] {message}";

        // Send the message with context to the chat service
        var response = await _chatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, prompt);

        // Track this message in context for future interactions
        // Note: We don't have direct access to SocketMessage here, but we can track the user's input
        // The bot's response will be tracked when it's sent as a regular message

        int messageCount = _messageCacheService.GetChatMessageCount(Context.Guild.Id);
        var hasContext = !string.IsNullOrWhiteSpace(context);

        // User Embed
        Embed userEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithAuthor(Context.User)
            .WithColor(Color.Teal)
            .Build();

        var botEmbeds = DiscordUtilities.ChunkMessageAsEmbeds(response, (chunk, chunkIndex, chunkCount) =>
        {
            var footerText = hasContext
                ? $"☁️ Using conversation context ☁️ {chunkIndex}/{chunkCount}"
                : $"☁️ Remembering last {messageCount} messages ☁️ {chunkIndex}/{chunkCount}";

            return new EmbedBuilder()
                .WithDescription(chunk)
                .WithAuthor(Context.Client.CurrentUser)
                .WithColor(Color.Purple)
                .WithFooter(footerText)
                .Build();
        }).ToList();


        if (botEmbeds.Count == 1)
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new Embed[] { userEmbed, botEmbeds.First() }; });
            return;
        }
        else
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new Embed[] { userEmbed }; });
            foreach (var botEmbed in botEmbeds)
            {
                await Context.Channel.SendMessageAsync(embed: botEmbed);
            }
        }
    }

    [SlashCommand("info", "Display bot information including version")]
    public async Task InfoAsync()
    {
        // Get version from assembly
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        // var amiquinBannerUrl = $"https://cdn.discordapp.com/banners/{Context.Client.CurrentUser.Id}/{Context.Client.CurrentUser.BannerId}?size=512";

        var amiquinBannerUrl = "https://cdn.discordapp.com/banners/1350616120838590464/ee9ef09c613404439b9fa64ee6cc6a7a?size=512";
        var embed = new EmbedBuilder()
            .WithTitle("☁️ Amiquin Bot Information")
            .WithDescription("A modular and extensible Discord bot")
            .WithColor(Color.Blue)
            .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
            .AddField("Version", assemblyVersion, true)
            .AddField("Bot ID", Context.Client.CurrentUser.Id.ToString(), true)
            .AddField("Created", Context.Client.CurrentUser.CreatedAt.ToString("MMM dd, yyyy"), true)
            .AddField("Servers", Context.Client.Guilds.Count.ToString(), true)
            .AddField("Users", Context.Client.Guilds.Sum(g => g.MemberCount).ToString(), true)
            .AddField("Shards", Context.Client.Shards.Count.ToString(), true)
            .WithFooter($"Requested by {Context.User.GlobalName ?? Context.User.Username}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithImageUrl(amiquinBannerUrl)
            .Build();

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
    }
}