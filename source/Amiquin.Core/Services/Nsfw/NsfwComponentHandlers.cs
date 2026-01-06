using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Scrappers;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Utilities;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Nsfw;

/// <summary>
/// Service for handling NSFW-related component interactions.
/// </summary>
public class NsfwComponentHandlers
{
    private readonly IComponentHandlerService _componentHandler;
    private readonly IFunService _funService;
    private readonly IScrapperManagerService _scrapperManager;
    private readonly IToggleService _toggleService;
    private readonly ILogger<NsfwComponentHandlers> _logger;

    public NsfwComponentHandlers(
        IComponentHandlerService componentHandler,
        IFunService funService,
        IScrapperManagerService scrapperManager,
        IToggleService toggleService,
        ILogger<NsfwComponentHandlers> logger)
    {
        _componentHandler = componentHandler;
        _funService = funService;
        _scrapperManager = scrapperManager;
        _toggleService = toggleService;
        _logger = logger;

        // Register component handlers
        RegisterNsfwHandlers();
    }

    private void RegisterNsfwHandlers()
    {
        // Register handlers for NSFW gallery interactions
        _componentHandler.RegisterHandler("nsfw_new_gallery", HandleNewGalleryAsync);
        _componentHandler.RegisterHandler("nsfw_random_waifu", HandleRandomWaifuAsync);
        _componentHandler.RegisterHandler("nsfw_gallery", HandleGalleryAsync);

        // Register handlers for parameterized NSFW category interactions
        _componentHandler.RegisterHandler("nsfw_random", HandleNsfwRandomAsync);
        _componentHandler.RegisterHandler("nsfw_get", HandleNsfwGetAsync);

        // Toggle buttons shown by /nsfw toggle (button-only UX)
        _componentHandler.RegisterHandler("nsfw_toggle_enable", HandleNsfwToggleAsync);
        _componentHandler.RegisterHandler("nsfw_toggle_disable", HandleNsfwToggleAsync);

        // Daily NSFW job buttons
        _componentHandler.RegisterHandler("nsfw_daily_refresh", HandleDailyRefreshAsync);
        _componentHandler.RegisterHandler("nsfw_daily_random", HandleDailyRandomAsync);

        _logger.LogDebug("Registered NSFW component handlers");
    }

    private async Task<bool> HandleNsfwRandomAsync(SocketMessageComponent component, ComponentContext context)
    {
        var nsfwType = context.Parameters.Length > 0 ? context.Parameters[0] : null;
        if (string.IsNullOrWhiteSpace(nsfwType))
        {
            await DiscordUtilities.SendErrorMessageAsync(component, "Invalid request", "Missing NSFW category.");
            return true;
        }

        return await HandleNsfwCategoryAsync(component, nsfwType);
    }

    private async Task<bool> HandleNsfwGetAsync(SocketMessageComponent component, ComponentContext context)
    {
        var nsfwType = context.Parameters.Length > 0 ? context.Parameters[0] : null;
        if (string.IsNullOrWhiteSpace(nsfwType))
        {
            await DiscordUtilities.SendErrorMessageAsync(component, "Invalid request", "Missing NSFW category.");
            return true;
        }

        return await HandleNsfwCategoryAsync(component, nsfwType);
    }

    private async Task<bool> HandleNsfwToggleAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            var serverId = component.GuildId ?? 0;
            if (serverId == 0)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Server Required", "This action can only be used in a server!");
                return true;
            }

            if (component.User is not SocketGuildUser guildUser || !guildUser.GuildPermissions.Administrator)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Permission Required", "You must be a server administrator to change NSFW settings.");
                return true;
            }

            var enable = component.Data.CustomId == "nsfw_toggle_enable";

            await _toggleService.SetServerToggleAsync(serverId, Constants.ToggleNames.EnableNSFW, enable,
                "Enable or disable NSFW content in this server");

            var isEnabled = await _funService.IsNsfwEnabledAsync(serverId);
            var status = isEnabled ? "enabled" : "disabled";
            var statusIcon = isEnabled ? "🔞" : "🚫";
            var statusColor = isEnabled ? "🔴" : "🟢";

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

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NSFW toggle button interaction");
            await DiscordUtilities.SendErrorMessageAsync(component, "Error", "Failed to update NSFW setting. Please try again.");
            return true;
        }
    }

    private async Task<bool> HandleDailyRefreshAsync(SocketMessageComponent component, ComponentContext context)
        => await HandleNewGalleryAsync(component, context);

    private async Task<bool> HandleDailyRandomAsync(SocketMessageComponent component, ComponentContext context)
        => await HandleRandomWaifuAsync(component, context);

    private async Task<bool> HandleNsfwCategoryAsync(SocketMessageComponent component, string nsfwType)
    {
        try
        {
            var serverId = component.GuildId ?? 0;
            if (serverId == 0)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Server Required", "NSFW commands can only be used in a server!");
                return true;
            }

            // Check if channel is NSFW
            if (component.Channel is Discord.ITextChannel textChannel && !textChannel.IsNsfw)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "NSFW Channel Required", "This command can only be used in NSFW channels!");
                return true;
            }

            // Check if NSFW is enabled for the server
            if (!await _funService.IsNsfwEnabledAsync(serverId))
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "NSFW content is disabled for this server.",
                    "An administrator can enable it using `/nsfw toggle enable:true`");
                return true;
            }

            var imageUrl = await _funService.GetNsfwGifAsync(serverId, nsfwType);

            if (string.IsNullOrEmpty(imageUrl))
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    $"🔧 **{nsfwType} Not Available Right Now** 🤖\n\n" +
                    $"The {nsfwType.ToLower()} service is having a temporary hiccup!\n\n" +
                    $"**Quick fixes to try:**\n" +
                    $"• Wait a minute and try again\n" +
                    $"• Try a different category with `/nsfw get`\n" +
                    $"• Check out the gallery with `/nsfw gallery`\n\n" +
                    $"*This usually resolves itself quickly!* ⚡");
                return true;
            }

            var title = nsfwType.Replace('_', ' ');

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 🔞 {title}\n## NSFW Content");
                    container.WithMediaGallery([imageUrl]);
                    container.WithTextDisplay($"*Requested by {component.User.Username}*");

                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🎲 Get Random**\nFetch another random image from this category"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Random")
                            .WithCustomId($"nsfw_random:{nsfwType}")
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
                            .WithCustomId($"nsfw_get:{nsfwType}")
                            .WithStyle(ButtonStyle.Secondary)));
                })
                .Build();

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling NSFW category button interaction ({NsfwType})", nsfwType);
            await DiscordUtilities.SendErrorMessageAsync(component, "Error", "Failed to fetch NSFW content. Please try again.");
            return true;
        }
    }

    private async Task<bool> HandleNewGalleryAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            var serverId = component.GuildId ?? 0;
            if (serverId == 0)
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "Server Required",
                    "NSFW commands can only be used in a server!");
                return true;
            }

            // Check if channel is NSFW
            if (component.Channel is Discord.ITextChannel textChannel && !textChannel.IsNsfw)
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "NSFW Channel Required",
                    "This command can only be used in NSFW channels!");
                return true;
            }

            // Check if NSFW is enabled for the server
            if (!await _funService.IsNsfwEnabledAsync(serverId))
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "NSFW content is disabled for this server.",
                    "An administrator can enable it using `/nsfw toggle enable:true`");
                return true;
            }

            // Check if any scrapers are available
            var availableScrapers = _scrapperManager.GetImageScrapers().ToList();
            if (!availableScrapers.Any())
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "Scrapper Service Unavailable",
                    "No image scrapers are currently enabled or configured.\n\n" +
                    "**What you can try:**\n" +
                    "• Contact a server administrator to enable scrapers\n" +
                    "• Try individual NSFW commands instead\n" +
                    "• Check back later when the service is available");
                return true;
            }

            // Fetch fresh images from multiple scrapers using gallery flow
            var imageUrls = await _scrapperManager.ScrapeGalleryImagesAsync(10, true, false); // Force fresh fetch

            if (imageUrls == null || imageUrls.Length == 0)
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "No Images Found",
                    "The scrapers couldn't find any images at this time.\n\n" +
                    "**What you can try:**\n" +
                    "• Wait a minute and try again\n" +
                    "• The source websites might be temporarily unavailable\n" +
                    "• Try again later for fresh content");
                return true;
            }

            // Create ComponentsV2 display with fresh gallery
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithMediaGallery(imageUrls);

                    var sourceNames = string.Join(", ", availableScrapers.Select(s => s.SourceName));
                    container.WithTextDisplay($"**Sources:** {sourceNames} || Count: {imageUrls.Length}");
                    container.WithTextDisplay($"*Fresh content from multiple sources • Updated just now*");

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

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling new gallery button interaction");
            await DiscordUtilities.SendErrorMessageAsync(component,
                "Scrapper Service Hiccup",
                "The image scrapper is having a temporary issue. This usually resolves itself quickly.\n\n" +
                "**What you can try:**\n" +
                "• Wait a minute and try again\n" +
                "• The source website might be temporarily unavailable\n" +
                "• Try again later for fresh scraped content");
            return true;
        }
    }

    private async Task<bool> HandleRandomWaifuAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            var serverId = component.GuildId ?? 0;
            if (serverId == 0)
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "Server Required",
                    "NSFW commands can only be used in a server!");
                return true;
            }

            // Check if channel is NSFW
            if (component.Channel is Discord.ITextChannel textChannel && !textChannel.IsNsfw)
            {
                await DiscordUtilities.SendErrorMessageAsync(component,
                    "NSFW Channel Required",
                    "This command can only be used in NSFW channels!");
                return true;
            }

            // Check if NSFW is enabled for the server
            if (!await _funService.IsNsfwEnabledAsync(serverId))
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ NSFW content is disabled for this server. An administrator can enable it using `/nsfw toggle enable:true`");
                return true;
            }

            // Get a random waifu image using the existing FunService
            var imageUrl = await _funService.GetNsfwGifAsync(serverId, "waifu");

            if (string.IsNullOrEmpty(imageUrl))
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    "🔧 **Waifu Not Available Right Now** 🤖\n\n" +
                    "The waifu service is having a temporary hiccup!\n\n" +
                    "**Quick fixes to try:**\n" +
                    "• Wait a minute and try again\n" +
                    "• Try the gallery for scraped content\n" +
                    "• Check back later if the issue persists\n\n" +
                    "*This usually resolves itself quickly!* ⚡");
                return true;
            }

            // Create ComponentsV2 display for single random waifu
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 🔞 Random Waifu\n## NSFW Content");

                    container.WithMediaGallery([imageUrl]);

                    container.WithTextDisplay($"*Random waifu for {component.User.Username}*");

                    // Add action buttons as sections
                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🎲 Another Random**\nGet another random waifu"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Another Random")
                            .WithCustomId("nsfw_random_waifu")
                            .WithStyle(ButtonStyle.Primary)));

                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent("**🖼️ View Gallery**\nSee a collection of scraped images"))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Gallery")
                            .WithCustomId("nsfw_gallery")
                            .WithStyle(ButtonStyle.Secondary)));
                })
                .Build();

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling random waifu button interaction");
            await component.ModifyOriginalResponseAsync(msg => msg.Content =
                "🚧 **Oops! Waifu Service Hiccup** 🤖\n\n" +
                "Something went wrong while fetching waifu content, but don't worry!\n\n" +
                "**What you can do:**\n" +
                "• Try again in 1-2 minutes\n" +
                "• Use the gallery for scraped content\n" +
                "• Check back later if the issue continues\n\n" +
                "*Most issues resolve themselves quickly!* 💙");
            return true;
        }
    }

    private async Task<bool> HandleGalleryAsync(SocketMessageComponent component, ComponentContext context)
    {
        // This is the same as HandleNewGalleryAsync - redirect to gallery
        return await HandleNewGalleryAsync(component, context);
    }
}