using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Scrappers;
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
    private readonly IScrapper _scrapper;
    private readonly ILogger<NsfwComponentHandlers> _logger;

    public NsfwComponentHandlers(
        IComponentHandlerService componentHandler,
        IFunService funService,
        IScrapper scrapper,
        ILogger<NsfwComponentHandlers> logger)
    {
        _componentHandler = componentHandler;
        _funService = funService;
        _scrapper = scrapper;
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

        _logger.LogDebug("Registered NSFW component handlers");
    }

    private async Task<bool> HandleNewGalleryAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            var serverId = component.GuildId ?? 0;
            if (serverId == 0)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW commands can only be used in a server!");
                return true;
            }

            // Check if channel is NSFW
            if (component.Channel is Discord.ITextChannel textChannel && !textChannel.IsNsfw)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in NSFW channels!");
                return true;
            }

            // Check if NSFW is enabled for the server
            if (!await _funService.IsNsfwEnabledAsync(serverId))
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ NSFW content is disabled for this server. An administrator can enable it using `/nsfw toggle enable:true`");
                return true;
            }

            // Check if scrapper is enabled
            if (!_scrapper.IsEnabled)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ **Scrapper Service Unavailable** 🚧\n\n" +
                    "The image scrapper is currently disabled or not configured.\n\n" +
                    "**What you can try:**\n" +
                    "• Contact a server administrator to enable the scrapper\n" +
                    "• Try individual NSFW commands instead\n" +
                    "• Check back later when the service is available\n\n" +
                    "*This service provides fresh content from various sources!* ⚡");
                return true;
            }

            // Fetch fresh images from scrapper
            var imageUrls = await _scrapper.GetImageUrlsAsync(10);

            if (imageUrls == null || imageUrls.Length == 0)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content =
                    "❌ **No Images Found** 🔍\n\n" +
                    "The scrapper couldn't find any images at this time.\n\n" +
                    "**What you can try:**\n" +
                    "• Wait a minute and try again\n" +
                    "• The source websites might be temporarily unavailable\n" +
                    "• Try again later for fresh content\n\n" +
                    "*Sometimes it just takes a moment for fresh content to load!* 🌟");
                return true;
            }

            // Create ComponentsV2 display with fresh gallery
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 🔞 NSFW Gallery")
                        .WithSeparator();

                    container.WithMediaGallery(imageUrls);

                    container.WithTextDisplay($"**Source:** {_scrapper.SourceName} || Count: {imageUrls.Length}");
                    container.WithTextDisplay($"*Fresh content from {_scrapper.SourceName} • Updated just now*");

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
            await component.ModifyOriginalResponseAsync(msg => msg.Content =
                "❌ **Oops! Scrapper service hiccup** 🤖\n\n" +
                "The image scrapper is having a temporary issue. This usually resolves itself quickly.\n\n" +
                "**What you can try:**\n" +
                "• Wait a minute and try again\n" +
                "• The source website might be temporarily unavailable\n" +
                "• Try again later for fresh scraped content\n\n" +
                "*Don't worry, this is usually just a temporary glitch!* ✨");
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
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW commands can only be used in a server!");
                return true;
            }

            // Check if channel is NSFW
            if (component.Channel is Discord.ITextChannel textChannel && !textChannel.IsNsfw)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in NSFW channels!");
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