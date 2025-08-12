using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands;

/// <summary>
/// Enum for NSFW content categories.
/// </summary>
public enum NsfwCategory
{
    // NSFW-only tags
    [ChoiceDisplay("Ero")]
    Ero,
    [ChoiceDisplay("Ass")]
    Ass,
    [ChoiceDisplay("Hentai")]
    Hentai,
    [ChoiceDisplay("MILF")]
    Milf,
    [ChoiceDisplay("Oral")]
    Oral,
    [ChoiceDisplay("Paizuri")]
    Paizuri,
    [ChoiceDisplay("Ecchi")]
    Ecchi,
    
    // Versatile tags (NSFW versions)
    [ChoiceDisplay("Waifu")]
    Waifu,
    [ChoiceDisplay("Maid")]
    Maid,
    [ChoiceDisplay("Oppai")]
    Oppai,
    [ChoiceDisplay("Selfies")]
    Selfies,
    [ChoiceDisplay("Uniform")]
    Uniform,
    
    // Character-specific
    [ChoiceDisplay("Marin Kitagawa")]
    Marin,
    [ChoiceDisplay("Mori Calliope")]
    Mori,
    [ChoiceDisplay("Raiden Shogun")]
    Raiden,
    [ChoiceDisplay("Kamisato Ayaka")]
    Ayaka
}

/// <summary>
/// NSFW commands for mature content (requires server toggle).
/// </summary>
[Group("nsfw", "NSFW content commands (18+ only, requires server toggle)")]
[RequireNsfw]
public class NsfwCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IFunService _funService;
    private readonly IToggleService _toggleService;
    private readonly INsfwApiService _nsfwApiService;

    public NsfwCommands(IFunService funService, IToggleService toggleService, INsfwApiService nsfwApiService)
    {
        _funService = funService;
        _toggleService = toggleService;
        _nsfwApiService = nsfwApiService;
    }

    [SlashCommand("get", "Get a random NSFW image of the specified category")]
    public async Task GetNsfwAsync(
        [Summary("category", "The type of NSFW content to fetch")] NsfwCategory category)
    {
        // Map enum to API tag and display name
        var (apiTag, displayName) = category switch
        {
            NsfwCategory.Ero => ("ero", "Ero"),
            NsfwCategory.Ass => ("ass", "Ass"),
            NsfwCategory.Hentai => ("hentai", "Hentai"),
            NsfwCategory.Milf => ("milf", "MILF"),
            NsfwCategory.Oral => ("oral", "Oral"),
            NsfwCategory.Paizuri => ("paizuri", "Paizuri"),
            NsfwCategory.Ecchi => ("ecchi", "Ecchi"),
            NsfwCategory.Waifu => ("waifu", "Waifu"),
            NsfwCategory.Maid => ("maid", "Maid"),
            NsfwCategory.Oppai => ("oppai", "Oppai"),
            NsfwCategory.Selfies => ("selfies", "Selfies"),
            NsfwCategory.Uniform => ("uniform", "Uniform"),
            NsfwCategory.Marin => ("marin-kitagawa", "Marin Kitagawa"),
            NsfwCategory.Mori => ("mori-calliope", "Mori Calliope"),
            NsfwCategory.Raiden => ("raiden-shogun", "Raiden Shogun"),
            NsfwCategory.Ayaka => ("kamisato-ayaka", "Kamisato Ayaka"),
            _ => ("waifu", "Waifu") // Default fallback
        };
        
        await HandleNsfwRequestAsync(apiTag, displayName);
    }

    [SlashCommand("gallery", "Get a collection of 10 random NSFW images")]
    public async Task GalleryAsync()
    {
        var serverId = Context.Guild?.Id ?? 0;
        
        if (serverId == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW commands can only be used in a server!");
            return;
        }

        // Check if channel is NSFW
        if (Context.Channel is ITextChannel textChannel && !textChannel.IsNsfw)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in NSFW channels!");
            return;
        }

        // Check if NSFW is enabled for the server
        if (!await _funService.IsNsfwEnabledAsync(serverId))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                "❌ NSFW content is disabled for this server. An administrator can enable it using `/nsfw toggle enable:true`");
            return;
        }

        try
        {
            // Fetch 10 random NSFW images (5 waifu, 5 alternative)
            var images = await _nsfwApiService.GetDailyNsfwImagesAsync(5, 5);
            
            if (images.Count == 0)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = 
                    "❌ Could not fetch NSFW gallery. Please try again later.");
                return;
            }

            // Build the gallery embed
            var embed = BuildNsfwGalleryEmbed(images);
            
            await ModifyOriginalResponseAsync(msg => 
            {
                msg.Content = null;
                msg.Embed = embed;
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                "❌ An error occurred while fetching the NSFW gallery. Please try again later.");
        }
    }

    [SlashCommand("toggle", "Check or toggle NSFW content for this server")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ToggleNsfwAsync(
        [Summary("enable", "Enable or disable NSFW content (leave empty to check status)")] bool? enable = null)
    {
        var serverId = Context.Guild?.Id ?? 0;
        
        if (serverId == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server!");
            return;
        }

        if (enable.HasValue)
        {
            // Toggle the NSFW setting
            await _toggleService.SetServerToggleAsync(serverId, Core.Constants.ToggleNames.EnableNSFW, enable.Value, 
                "Enable or disable NSFW content in this server");
            
            var status = enable.Value ? "enabled" : "disabled";
            await ModifyOriginalResponseAsync(msg => msg.Content = $"✅ NSFW content has been **{status}** for this server!");
        }
        else
        {
            // Check current status
            var isEnabled = await _funService.IsNsfwEnabledAsync(serverId);
            var status = isEnabled ? "enabled" : "disabled";
            
            var embed = new EmbedBuilder()
                .WithTitle("NSFW Status")
                .WithDescription($"NSFW content is currently **{status}** for this server.")
                .WithColor(isEnabled ? Color.Red : Color.Green)
                .AddField("How to change", 
                    "Server administrators can use `/nsfw toggle enable:true` or `/nsfw toggle enable:false` to change this setting.")
                .WithCurrentTimestamp()
                .Build();
            
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
    }

    /// <summary>
    /// Generic handler for NSFW content requests.
    /// </summary>
    private async Task HandleNsfwRequestAsync(string nsfwType, string displayName)
    {
        var serverId = Context.Guild?.Id ?? 0;
        
        if (serverId == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW commands can only be used in a server!");
            return;
        }

        // Check if channel is NSFW
        if (Context.Channel is ITextChannel textChannel && !textChannel.IsNsfw)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in NSFW channels!");
            return;
        }

        // Check if NSFW is enabled for the server
        if (!await _funService.IsNsfwEnabledAsync(serverId))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                "❌ NSFW content is disabled for this server. An administrator can enable it using `/nsfw toggle enable:true`");
            return;
        }

        try
        {
            var imageUrl = await _funService.GetNsfwGifAsync(serverId, nsfwType);
            
            if (string.IsNullOrEmpty(imageUrl))
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = 
                    $"❌ Could not fetch {displayName} content. Please try again later.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"🔞 {displayName}")
                .WithImageUrl(imageUrl)
                .WithColor(Color.Red)
                .WithFooter($"Requested by {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                $"❌ An error occurred while fetching {displayName} content. Please try again later.");
        }
    }

    /// <summary>
    /// Builds an embed for displaying a gallery of NSFW images.
    /// </summary>
    private Embed BuildNsfwGalleryEmbed(List<Core.Models.NsfwImage> images)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle("🔞 NSFW Gallery")
            .WithDescription($"A collection of {images.Count} random NSFW images")
            .WithColor(new Color(255, 0, 100))
            .WithCurrentTimestamp()
            .WithFooter($"Requested by {Context.User.Username} • Enjoy responsibly", Context.User.GetAvatarUrl());

        // Display first image as the main image
        if (images.Count > 0)
        {
            embedBuilder.WithImageUrl(images[0].Url);
        }

        // Add up to 10 images as fields with links
        for (int i = 0; i < Math.Min(images.Count, 10); i++)
        {
            var img = images[i];
            var fieldTitle = $"Image {i + 1}";
            
            // Create a compact field value with source and link
            var fieldValue = $"[{img.Source ?? "View"}]({img.Url})";
            
            if (!string.IsNullOrWhiteSpace(img.Artist))
            {
                fieldValue += $" • Artist: {img.Artist}";
            }
            
            // Add tags if available (limit to first 50 chars)
            if (!string.IsNullOrWhiteSpace(img.Tags))
            {
                var tags = img.Tags.Length > 50 ? img.Tags.Substring(0, 47) + "..." : img.Tags;
                fieldValue += $"\nTags: {tags}";
            }
            
            embedBuilder.AddField(fieldTitle, fieldValue, inline: true);
        }

        // Add a note about the gallery
        embedBuilder.AddField("ℹ️ Gallery Info", 
            $"This gallery contains {images.Count} images from various sources.\n" +
            "Click the links to view each image in full resolution.", 
            inline: false);

        return embedBuilder.Build();
    }
}