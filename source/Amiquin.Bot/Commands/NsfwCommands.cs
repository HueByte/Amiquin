using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<NsfwCommands> _logger;

    public NsfwCommands(IFunService funService, IToggleService toggleService, INsfwApiService nsfwApiService, ILogger<NsfwCommands> logger)
    {
        _funService = funService;
        _toggleService = toggleService;
        _nsfwApiService = nsfwApiService;
        _logger = logger;
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
            // Fetch 10 random NSFW images with status information
            var result = await _nsfwApiService.GetNsfwImagesWithStatusAsync(5, 5);
            
            if (!result.IsSuccess)
            {
                await ModifyOriginalResponseAsync(msg => 
                {
                    msg.Content = CreateGracefulErrorMessage(result);
                    msg.Embed = null;
                });
                return;
            }

            // Build the gallery using ComponentsV2 MediaGallery
            var components = BuildNsfwGalleryComponents(result.Images);
            
            // Add status message if there were issues but we still got some images
            var statusMessage = result.IsTemporaryFailure && !string.IsNullOrEmpty(result.ErrorMessage)
                ? $"⚠️ {result.ErrorMessage}" 
                : null; // Message content is now in the ComponentsV2 display
            
            await ModifyOriginalResponseAsync(msg => 
            {
                msg.Content = statusMessage;
                msg.Embed = null; // Clear any existing embed
                msg.Components = components;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NSFW gallery command");
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                "❌ **Oops! Something went wrong** 🤖\n\n" +
                "The NSFW gallery service is having a temporary hiccup. This usually resolves itself quickly.\n\n" +
                "**What you can try:**\n" +
                "• Wait a minute and try again\n" +
                "• Use individual NSFW commands instead\n" +
                "• Try again later if the issue persists\n\n" +
                "*Don't worry, this is usually just a temporary glitch!* ✨");
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
                    $"🔧 **{displayName} Not Available Right Now** 🤖\n\n" +
                    $"The {displayName.ToLower()} service is having a temporary hiccup!\n\n" +
                    $"**Quick fixes to try:**\n" +
                    $"• Wait a minute and try again\n" +
                    $"• Try a different category with `/nsfw get`\n" +
                    $"• Check out the gallery with `/nsfw gallery`\n\n" +
                    $"*This usually resolves itself quickly!* ⚡");
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
                $"🚧 **Oops! {displayName} Service Hiccup** 🤖\n\n" +
                $"Something went wrong while fetching {displayName.ToLower()} content, but don't worry!\n\n" +
                $"**What you can do:**\n" +
                $"• Try again in 1-2 minutes\n" +
                $"• Use `/nsfw gallery` for a variety pack\n" +
                $"• Try different categories - they might work better\n" +
                $"• Check back later if the issue continues\n\n" +
                $"*Most issues resolve themselves quickly!* 💙");
        }
    }

    /// <summary>
    /// Builds ComponentsV2 with native MediaGallery for displaying NSFW images.
    /// </summary>
    private MessageComponent BuildNsfwGalleryComponents(List<Core.Models.NsfwImage> images)
    {
        // Extract image URLs for the media gallery (limit to 10 as per Discord's MediaGallery limit)
        var imageUrls = images.Take(10).Select(img => img.Url).ToArray();
        
        // Build source information for display
        var sourceInfo = string.Join(", ", images
            .Select(img => img.Source ?? "Unknown")
            .Distinct()
            .Take(3));
        
        var artistInfo = images
            .Where(img => !string.IsNullOrWhiteSpace(img.Artist))
            .Select(img => img.Artist!)
            .Distinct()
            .Take(3)
            .ToList();

        var artistText = artistInfo.Count > 0 
            ? $"\n-# Artists: {string.Join(", ", artistInfo)}{(artistInfo.Count == 3 ? " and others" : "")}"
            : "";

        return new ComponentBuilderV2()
            .WithTextDisplay($"# 🔞 NSFW Gallery")
            .WithTextDisplay($"-# {images.Count} images • Sources: {sourceInfo}{artistText}")
            .WithMediaGallery(imageUrls)
            .WithTextDisplay($"-# Requested by {Context.User.Username} • Enjoy responsibly")
            .Build();
    }

    /// <summary>
    /// Creates a graceful error message for users based on the API result status.
    /// </summary>
    private string CreateGracefulErrorMessage(NsfwApiResult result)
    {
        if (result.IsRateLimited)
        {
            var waitTimeMinutes = result.RetryAfter.HasValue 
                ? Math.Ceiling((result.RetryAfter.Value - DateTime.UtcNow).TotalMinutes)
                : 5;
            
            return $"⏳ **Taking a quick break!** 🎯\n\n" +
                   $"The NSFW image services are currently rate-limited to ensure fair usage for everyone.\n\n" +
                   $"**When can you try again?**\n" +
                   $"• In about {waitTimeMinutes} minute(s)\n" +
                   $"• Individual NSFW commands may still work\n\n" +
                   $"*Thanks for your patience! This helps keep the service stable for everyone.* 💙";
        }

        if (result.IsTemporaryFailure)
        {
            return "🔧 **Temporary Service Hiccup** 🤖\n\n" +
                   "The NSFW image services are experiencing some temporary issues, but they should be back soon!\n\n" +
                   "**What you can try:**\n" +
                   "• Wait 2-3 minutes and try again\n" +
                   "• Use individual `/nsfw get` commands which might work better\n" +
                   "• Try different categories if some aren't working\n\n" +
                   "*Most issues resolve themselves within a few minutes!* ⚡";
        }

        return "❌ **Service Temporarily Unavailable** 🚧\n\n" +
               "The NSFW gallery is currently experiencing issues and couldn't fetch any images.\n\n" +
               "**Don't worry, this is usually temporary:**\n" +
               "• The service should be back within 5-10 minutes\n" +
               "• You can try individual NSFW commands instead\n" +
               "• Check back later if the issue persists\n\n" +
               "*We're working to keep everything running smoothly!* 🛠️";
    }
}