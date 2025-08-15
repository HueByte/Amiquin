using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Scrappers;
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
    private readonly IScrapper _scrapper;
    private readonly ILogger<NsfwCommands> _logger;

    public NsfwCommands(IFunService funService, IToggleService toggleService, INsfwApiService nsfwApiService, IScrapper scrapper, ILogger<NsfwCommands> logger)
    {
        _funService = funService;
        _toggleService = toggleService;
        _nsfwApiService = nsfwApiService;
        _scrapper = scrapper;
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
            // Check if scrapper is enabled
            if (!_scrapper.IsEnabled)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ **Scrapper Service Unavailable** 🚧\n\n" +
                    "The image scrapper is currently disabled or not configured.\n\n" +
                    "**What you can try:**\n" +
                    "• Contact a server administrator to enable the scrapper\n" +
                    "• Try individual NSFW commands instead\n" +
                    "• Check back later when the service is available\n\n" +
                    "*This service provides fresh content from various sources!* ⚡");
                return;
            }

            // Fetch 10 image URLs directly from scrapper
            var imageUrls = await _scrapper.GetImageUrlsAsync(10);

            if (imageUrls == null || imageUrls.Length == 0)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ **No Images Found** 🔍\n\n" +
                    "The scrapper couldn't find any images at this time.\n\n" +
                    "**What you can try:**\n" +
                    "• Wait a minute and try again\n" +
                    "• The source websites might be temporarily unavailable\n" +
                    "• Try again later for fresh content\n\n" +
                    "*Sometimes it just takes a moment for fresh content to load!* 🌟");
                return;
            }

            // Create ComponentsV2 display with media gallery
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithMediaGallery(imageUrls);

                    container.WithTextDisplay($"**Source:** {_scrapper.SourceName} || Count: {imageUrls.Length}");
                    container.WithTextDisplay($"*Requested by {Context.User.Username} • Fresh content from {_scrapper.SourceName}*");

                    // Add action buttons as sections
                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🔄 New Gallery**\nScrape a fresh collection of images"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("New Gallery")
                            .WithCustomId("nsfw_new_gallery")
                            .WithStyle(ButtonStyle.Primary)));

                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🎲 Random Image**\nGet a single random image"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Random Image")
                            .WithCustomId("nsfw_random_waifu")
                            .WithStyle(ButtonStyle.Secondary)));
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in NSFW gallery scrapper command");
            await ModifyOriginalResponseAsync(msg => msg.Content =
                "❌ **Oops! Scrapper service hiccup** 🤖\n\n" +
                "The image scrapper is having a temporary issue. This usually resolves itself quickly.\n\n" +
                "**What you can try:**\n" +
                "• Wait a minute and try again\n" +
                "• The source website might be temporarily unavailable\n" +
                "• Try again later for fresh scraped content\n\n" +
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
            var statusIcon = isEnabled ? "🔞" : "🚫";
            var statusColor = isEnabled ? "🔴" : "🟢";

            // Create ComponentsV2 display for NSFW status
            var components = new ComponentBuilderV2()
                .WithTextDisplay($"# {statusIcon} NSFW Status\n## Currently {status}")
                .WithTextDisplay($"**Status:** {statusColor} NSFW content is **{status}** for this server")
                .WithTextDisplay("**How to change:** Server administrators can use `/nsfw toggle enable:true` or `/nsfw toggle enable:false` to change this setting")
                .WithActionRow([
                    new ButtonBuilder()
                        .WithLabel(isEnabled ? "🚫 Disable NSFW" : "🔞 Enable NSFW")
                        .WithCustomId(isEnabled ? "nsfw_toggle_disable" : "nsfw_toggle_enable")
                        .WithStyle(isEnabled ? ButtonStyle.Danger : ButtonStyle.Success)
                ])
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });
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

            // Create ComponentsV2 display for NSFW content
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 🔞 {displayName}\n## NSFW Content");

                    container.WithMediaGallery([imageUrl]);

                    container.WithTextDisplay($"*Requested by {Context.User.Username}*");

                    // Add action buttons as sections
                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🎲 Get Random**\nFetch another random image from this category"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Random")
                            .WithCustomId($"nsfw_random_{nsfwType}")
                            .WithStyle(ButtonStyle.Primary)));

                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🖼️ View Gallery**\nSee a collection of multiple images"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Gallery")
                            .WithCustomId("nsfw_gallery")
                            .WithStyle(ButtonStyle.Secondary)));

                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🔄 New Image**\nGet a different image from this category"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("New Image")
                            .WithCustomId($"nsfw_get_{nsfwType}")
                            .WithStyle(ButtonStyle.Secondary)));
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });
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