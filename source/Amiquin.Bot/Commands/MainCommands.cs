using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Fun;
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
    private readonly IFunService _funService;

    public MainCommands(
        IPersonaChatService chatService, 
        IMessageCacheService messageCacheService, 
        IChatContextService chatContextService,
        IFunService funService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _chatContextService = chatContextService;
        _funService = funService;
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

        // If response is empty, it means the request was silently skipped (duplicate/busy)
        if (string.IsNullOrEmpty(response))
        {
            // Silently delete the deferred response to avoid any message
            await DeleteOriginalResponseAsync();
            return;
        }

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

    #region Fun Commands
    
    [SlashCommand("size", "Check your... size 📏")]
    public async Task SizeAsync([Summary("user", "User to check (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        
        try
        {
            var size = await _funService.GetOrGenerateDickSizeAsync(targetUser.Id, Context.Guild.Id);
            
            var sizeDescription = size switch
            {
                <= 5 => "🤏 Nano",
                <= 10 => "😬 Small",
                <= 15 => "😐 Average",
                <= 20 => "😎 Above Average",
                <= 25 => "🍆 Large",
                _ => "🐋 MASSIVE"
            };
            
            var embed = new EmbedBuilder()
                .WithTitle("📏 Size Check")
                .WithDescription($"{targetUser.Mention}'s size: **{size} cm** {sizeDescription}")
                .WithColor(Color.Purple)
                .WithFooter("Results are permanent and totally scientific 🧪")
                .Build();
                
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to measure size. Try again later!");
        }
    }
    
    [SlashCommand("color", "Display a hex color")]
    public async Task ColorAsync([Summary("hex", "Hex color code (e.g., #FF5733 or FF5733)")] string hexColor)
    {
        try
        {
            using var colorImage = await _funService.GenerateColorImageAsync(hexColor);
            
            var cleanHex = hexColor.TrimStart('#').ToUpper();
            var attachment = new FileAttachment(colorImage, $"color_{cleanHex}.png");
            
            var embed = new EmbedBuilder()
                .WithTitle($"🎨 Color: #{cleanHex}")
                .WithImageUrl($"attachment://color_{cleanHex}.png")
                .WithColor(uint.Parse(cleanHex, System.Globalization.NumberStyles.HexNumber))
                .Build();
                
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Attachments = new[] { attachment };
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid hex color format. Use #RRGGBB or RRGGBB format.");
        }
    }
    
    [SlashCommand("palette", "Generate a random color palette")]
    public async Task PaletteAsync([Summary("count", "Number of colors (1-10)")] int count = 5)
    {
        if (count < 1 || count > 10)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Count must be between 1 and 10.");
            return;
        }
        
        try
        {
            var colors = _funService.GenerateColorPalette(count);
            
            var embed = new EmbedBuilder()
                .WithTitle($"🎨 Random Color Palette ({count} colors)")
                .WithColor(Color.Gold);
                
            foreach (var color in colors)
            {
                var colorValue = uint.Parse(color.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
                embed.AddField(color.ToUpper(), $"RGB: {(colorValue >> 16) & 255}, {(colorValue >> 8) & 255}, {colorValue & 255}", true);
            }
            
            embed.WithDescription(string.Join(" ", colors.Select(c => $"`{c}`")));
            
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to generate color palette. Try again later!");
        }
    }
    
    [SlashCommand("avatar", "Get a user's avatar")]
    public async Task AvatarAsync([Summary("user", "User to get avatar from (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        
        var embed = new EmbedBuilder()
            .WithTitle($"🖼️ {targetUser.GlobalName ?? targetUser.Username}'s Avatar")
            .WithImageUrl(targetUser.GetDisplayAvatarUrl(ImageFormat.Auto, 512))
            .WithColor(Color.Blue)
            .WithFooter($"Requested by {Context.User.GlobalName ?? Context.User.Username}")
            .Build();
            
        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
    }
    
    [SlashCommand("nacho", "Give Amiquin a nacho! 🌮")]
    public async Task NachoAsync()
    {
        try
        {
            // Give the nacho and get the total count
            var totalNachos = await _funService.GiveNachoAsync(Context.User.Id, Context.Guild.Id);
            
            // Generate a dynamic, context-aware response using AI
            var response = await _funService.GenerateNachoResponseAsync(
                Context.User.Id, 
                Context.Guild.Id, 
                Context.Channel.Id,
                Context.User.Username,
                totalNachos
            );
            
            // Build the embed with the dynamic response
            var embed = new EmbedBuilder()
                .WithTitle("Nacho Delivery! 🌮")
                .WithDescription($"{response}\n\n**Your total nachos given:** {totalNachos}")
                .WithColor(Color.Orange)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
                .WithFooter($"Nacho #{totalNachos} from {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();
                
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to deliver nacho. Try again later!");
        }
    }
    
    [SlashCommand("nacho-leaderboard", "View the nacho leaderboard")]
    public async Task NachoLeaderboardAsync()
    {
        try
        {
            var leaderboardFields = await _funService.GetNachoLeaderboardAsync(Context.Guild.Id, 10);
            var totalNachos = await _funService.GetTotalNachosAsync(Context.Guild.Id);
            
            var embed = new EmbedBuilder()
                .WithTitle($"🏆 Nacho Leaderboard")
                .WithDescription($"**Total nachos received:** {totalNachos} 🌮")
                .WithColor(Color.Gold)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl());
                
            if (!leaderboardFields.Any())
            {
                embed.AddField("No nachos yet!", "Be the first to give me a nacho with `/nacho`! 🌮", false);
            }
            else
            {
                foreach (var field in leaderboardFields.Take(10))
                {
                    embed.AddField(field.Name, field.Value, field.IsInline);
                }
            }
            
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to load nacho leaderboard. Try again later!");
        }
    }
    
    #endregion

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