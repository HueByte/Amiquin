using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using System.Reflection;
namespace Amiquin.Bot.Commands;

public class MainCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IPersonaChatService _chatService;
    private readonly IDiscordBotChatService _discordBotChatService;
    private readonly IMessageCacheService _messageCacheService;

    public MainCommands(IPersonaChatService chatService, IDiscordBotChatService discordBotChatService, IMessageCacheService messageCacheService)
    {
        _chatService = chatService;
        _discordBotChatService = discordBotChatService;
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

    [SlashCommand("info", "Display bot information including version")]
    public async Task InfoAsync()
    {
        await DeferAsync();

        // Get version from assembly
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

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
            .Build();

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
    }

    [SlashCommand("chat-new", "Chat with the bot using improved session management")]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ChatNewAsync(string message)
    {
        await DeferAsync();
        
        try
        {
            var response = await _discordBotChatService.ProcessMessageAsync(
                message, 
                Context.User.Id, 
                Context.Channel.Id, 
                Context.Guild?.Id);

            // Split long responses if needed (Discord has 2000 character limit)
            await RespondWithSplitMessageAsync(response);
        }
        catch (Exception ex)
        {
            await FollowupAsync("Sorry, I encountered an error processing your message.");
        }
    }

    [SlashCommand("clear-chat", "Clear your conversation history")]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ClearChatAsync()
    {
        try
        {
            await _discordBotChatService.ClearConversationAsync(
                Context.User.Id, 
                Context.Channel.Id, 
                Context.Guild?.Id);
                
            await RespondAsync("Your conversation history has been cleared! 🗑️", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondAsync("Failed to clear conversation history.", ephemeral: true);
        }
    }

    [SlashCommand("chat-stats", "View your conversation statistics")]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ChatStatsAsync()
    {
        await DeferAsync();
        
        try
        {
            var stats = await _discordBotChatService.GetConversationStatsAsync(
                Context.User.Id, 
                Context.Channel.Id, 
                Context.Guild?.Id);
                
            var embed = new EmbedBuilder()
                .WithTitle("📊 Conversation Statistics")
                .AddField("Messages", stats.MessageCount, true)
                .AddField("Session Started", stats.StartTime.ToString("yyyy-MM-dd HH:mm"), true)
                .AddField("Last Message", stats.LastMessageTime.ToString("yyyy-MM-dd HH:mm"), true)
                .AddField("Estimated Tokens", stats.TotalTokensUsed, true)
                .WithColor(Color.Blue)
                .WithFooter($"Session: {Context.User.Id}:{Context.Channel.Id}")
                .WithTimestamp(DateTimeOffset.Now)
                .Build();
                
            await FollowupAsync(embed: embed, ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync("Failed to retrieve conversation statistics.", ephemeral: true);
        }
    }

    private async Task RespondWithSplitMessageAsync(string message)
    {
        const int maxLength = 1950; // Leave some buffer for Discord's 2000 limit
        
        if (message.Length <= maxLength)
        {
            await FollowupAsync(message);
            return;
        }
        
        // Split message at word boundaries
        var parts = new List<string>();
        var currentPart = "";
        var words = message.Split(' ');
        
        foreach (var word in words)
        {
            if (currentPart.Length + word.Length + 1 > maxLength)
            {
                parts.Add(currentPart.Trim());
                currentPart = word;
            }
            else
            {
                currentPart += " " + word;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(currentPart))
        {
            parts.Add(currentPart.Trim());
        }
        
        // Send first part as followup, rest as new messages
        await FollowupAsync(parts[0]);
        
        for (int i = 1; i < parts.Count; i++)
        {
            await Context.Channel.SendMessageAsync(parts[i]);
        }
    }
}