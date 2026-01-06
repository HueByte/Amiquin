using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Toggle;
using Discord;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using Color = SixLabors.ImageSharp.Color;

namespace Amiquin.Core.Services.Fun;

/// <summary>
/// Service implementation for handling fun commands and interactions.
/// </summary>
public class FunService : IFunService
{
    private readonly IUserStatsRepository _userStatsRepository;
    private readonly IToggleService _toggleService;
    private readonly IPersonaChatService _personaChatService;
    private readonly IComponentHandlerService _componentHandler;
    private readonly ILogger<FunService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    // GIF API endpoints for different interactions
    private readonly Dictionary<string, string> _gifApiEndpoints = new()
    {
        { "bite", "https://api.waifu.pics/sfw/bite" },
        { "kiss", "https://api.waifu.pics/sfw/kiss" },
        { "hug", "https://api.waifu.pics/sfw/hug" },
        { "slap", "https://api.waifu.pics/sfw/slap" },
        { "pat", "https://api.waifu.pics/sfw/pat" },
        { "poke", "https://api.waifu.pics/sfw/poke" },
        { "wave", "https://api.waifu.pics/sfw/wave" },
        { "highfive", "https://api.waifu.pics/sfw/highfive" }
    };

    // NSFW tags for waifu.im API (requires server toggle)
    private readonly Dictionary<string, string> _nsfwTags = new()
    {
        // NSFW-only tags
        { "ero", "Erotic content" },
        { "ass", "Ass focused content" },
        { "hentai", "Hentai content" },
        { "milf", "MILF content" },
        { "oral", "Oral content" },
        { "paizuri", "Paizuri content" },
        { "ecchi", "Ecchi content" }
    };

    // Versatile tags that can be used with is_nsfw parameter
    private readonly Dictionary<string, string> _versatileTags = new()
    {
        { "waifu", "Waifu images" },
        { "maid", "Maid themed" },
        { "marin-kitagawa", "Marin Kitagawa character" },
        { "mori-calliope", "Mori Calliope character" },
        { "raiden-shogun", "Raiden Shogun character" },
        { "oppai", "Oppai focused" },
        { "selfies", "Selfie style images" },
        { "uniform", "Uniform themed" },
        { "kamisato-ayaka", "Kamisato Ayaka character" }
    };

    public FunService(
        IUserStatsRepository userStatsRepository,
        IToggleService toggleService,
        IPersonaChatService personaChatService,
        IComponentHandlerService componentHandler,
        ILogger<FunService> logger,
        HttpClient httpClient)
    {
        _userStatsRepository = userStatsRepository;
        _toggleService = toggleService;
        _personaChatService = personaChatService;
        _componentHandler = componentHandler;
        _logger = logger;
        _httpClient = httpClient;

        // Register component handlers
        RegisterColorHandlers();
    }

    /// <inheritdoc/>
    public async Task<int> GetOrGenerateDickSizeAsync(ulong userId, ulong serverId)
    {
        var userStats = await _userStatsRepository.GetOrCreateUserStatsAsync(userId, serverId);

        if (!userStats.HasStat("dick_size"))
        {
            // Generate a random size between 1 and 30 cm with weighted distribution
            // Most results will be in the 10-20 range, with rare extremes
            var roll1 = _random.Next(1, 31);
            var roll2 = _random.Next(1, 31);
            var roll3 = _random.Next(1, 31);

            // Take the middle value for more realistic distribution
            var sizes = new[] { roll1, roll2, roll3 }.OrderBy(x => x).ToArray();
            var dickSize = sizes[1]; // Middle value

            userStats.SetStat("dick_size", dickSize);
            await _userStatsRepository.UpdateUserStatsAsync(userStats);
            _logger.LogDebug("Generated dick size {Size} for user {UserId}", dickSize, userId);
        }

        return userStats.GetStat<int>("dick_size");
    }

    /// <inheritdoc/>
    public async Task<Stream> GenerateColorImageAsync(string hexColor)
    {
        try
        {
            // Clean up the hex color input
            hexColor = hexColor.TrimStart('#');
            if (hexColor.Length != 6)
            {
                throw new ArgumentException("Invalid hex color format. Expected format: #RRGGBB or RRGGBB");
            }

            // Parse the hex color
            if (!Rgba32.TryParseHex($"#{hexColor}", out var color))
            {
                throw new ArgumentException($"Invalid hex color: #{hexColor}");
            }

            // Create a 300x100 image with the color
            using var image = new Image<Rgba32>(300, 100);

            // Fill with the color and add border
            image.Mutate(ctx => ctx
                .BackgroundColor(color)
                .Draw(Color.Black, 2f, new RectangleF(1, 1, 298, 98)));

            // Add text with color info (if fonts are available)
            var font = GetAvailableFont(12, FontStyle.Bold);
            if (font != null)
            {
                var textColor = GetContrastColor(color);
                var text = $"#{hexColor.ToUpper()}";

                var textOptions = new RichTextOptions(font)
                {
                    Origin = new PointF(150, 50),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                image.Mutate(ctx => ctx
                    .DrawText(textOptions, text, textColor));
            }
            else
            {
                _logger.LogWarning("No fonts available - generating color image without text for hex {HexColor}", hexColor);
            }

            // Convert to stream
            var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            stream.Position = 0;

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating color image for hex {HexColor}", hexColor);
            throw;
        }
    }

    /// <inheritdoc/>
    public List<string> GenerateColorPalette(int count = 5)
    {
        var colors = new List<string>();

        for (int i = 0; i < count; i++)
        {
            // Generate random RGB values
            var r = _random.Next(0, 256);
            var g = _random.Next(0, 256);
            var b = _random.Next(0, 256);

            // Convert to hex
            var hex = $"#{r:X2}{g:X2}{b:X2}";
            colors.Add(hex);
        }

        return colors;
    }

    /// <inheritdoc/>
    public async Task<Stream> GeneratePaletteImageAsync(List<string> colors)
    {
        try
        {
            if (colors == null || colors.Count == 0)
            {
                throw new ArgumentException("Color list cannot be null or empty");
            }

            // Image dimensions
            const int swatchWidth = 80;
            const int swatchHeight = 80;
            const int margin = 10;
            const int textHeight = 25;

            var font = GetAvailableFont(11, FontStyle.Regular);
            var includeText = font != null;

            var imageWidth = colors.Count * (swatchWidth + margin) - margin;
            var imageHeight = swatchHeight + (includeText ? textHeight : 0) + margin * 2;

            using var image = new Image<Rgba32>(imageWidth, imageHeight);

            // Fill background with white
            image.Mutate(ctx => ctx.BackgroundColor(Color.White));

            if (!includeText)
            {
                _logger.LogWarning("No fonts available - generating palette image without text labels");
            }

            for (int i = 0; i < colors.Count; i++)
            {
                var colorHex = colors[i].TrimStart('#');

                // Parse the color
                if (!Rgba32.TryParseHex($"#{colorHex}", out var color))
                {
                    _logger.LogWarning("Invalid hex color in palette: {Color}", colors[i]);
                    continue;
                }

                var x = i * (swatchWidth + margin);
                var swatchRect = new RectangleF(x, margin, swatchWidth, swatchHeight);

                // Draw color swatch
                image.Mutate(ctx => ctx
                    .Fill(color, swatchRect)
                    .Draw(Color.Black, 1f, swatchRect));

                // Draw hex code below swatch (if fonts are available)
                if (includeText && font != null)
                {
                    var textX = x + swatchWidth / 2;
                    var textY = margin + swatchHeight + 5;

                    var textOptions = new RichTextOptions(font)
                    {
                        Origin = new PointF(textX, textY),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    image.Mutate(ctx => ctx
                        .DrawText(textOptions, $"#{colorHex.ToUpper()}", Color.Black));
                }
            }

            // Convert to stream
            var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            stream.Position = 0;

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating palette image");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetInteractionGifAsync(string interactionType)
    {
        try
        {
            var endpoint = _gifApiEndpoints.GetValueOrDefault(interactionType.ToLower());
            if (endpoint == null)
            {
                _logger.LogWarning("Unknown interaction type: {InteractionType}", interactionType);
                return null;
            }

            var response = await _httpClient.GetStringAsync(endpoint);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
            {
                return urlElement.GetString();
            }

            _logger.LogWarning("No URL found in response for interaction type: {InteractionType}", interactionType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GIF for interaction type: {InteractionType}", interactionType);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetNsfwGifAsync(ulong serverId, string nsfwType)
    {
        try
        {
            // Check if NSFW is enabled for this server
            if (!await IsNsfwEnabledAsync(serverId))
            {
                _logger.LogWarning("NSFW content requested but disabled for server {ServerId}", serverId);
                return null;
            }

            var tagLower = nsfwType.ToLower();

            // Check if it's a valid tag (either NSFW or versatile)
            bool isNsfwTag = _nsfwTags.ContainsKey(tagLower);
            bool isVersatileTag = _versatileTags.ContainsKey(tagLower);

            if (!isNsfwTag && !isVersatileTag)
            {
                _logger.LogWarning("Unknown NSFW type: {NsfwType}", nsfwType);
                return null;
            }

            // Build the waifu.im API URL
            var url = $"https://api.waifu.im/search?included_tags={tagLower}";

            // Add is_nsfw parameter for versatile tags
            if (isVersatileTag)
            {
                url += "&is_nsfw=true";
            }

            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            // waifu.im returns an object with an "images" array
            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesElement) &&
                imagesElement.GetArrayLength() > 0)
            {
                var firstImage = imagesElement[0];
                if (firstImage.TryGetProperty("url", out var urlElement))
                {
                    return urlElement.GetString();
                }
            }

            _logger.LogWarning("No images found in waifu.im response for tag: {NsfwType}", nsfwType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NSFW image for tag: {NsfwType}", nsfwType);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsNsfwEnabledAsync(ulong serverId)
    {
        return await _toggleService.IsEnabledAsync(serverId, Constants.ToggleNames.EnableNSFW);
    }

    /// <inheritdoc/>
    public List<string> GetAvailableNsfwTypes()
    {
        // Combine both NSFW-only and versatile tags
        var allTags = new List<string>();
        allTags.AddRange(_nsfwTags.Keys);
        allTags.AddRange(_versatileTags.Keys);
        return allTags;
    }

    /// <inheritdoc/>
    public async Task<int> GiveNachoAsync(ulong userId, ulong serverId)
    {
        var userStats = await _userStatsRepository.GetOrCreateUserStatsAsync(userId, serverId);
        var newTotal = userStats.IncrementStat("nachos_given");
        await _userStatsRepository.UpdateUserStatsAsync(userStats);

        _logger.LogDebug("User {UserId} gave a nacho to Amiquin. Total: {Total}", userId, newTotal);
        return newTotal;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateNachoResponseAsync(ulong userId, ulong serverId, ulong channelId, string userName, int totalNachos)
    {
        try
        {
            // Create a context-aware prompt for the nacho interaction
            var prompt = $"The user {userName} just gave you a delicious nacho! 🌮 " +
                        $"This is nacho number {totalNachos} they've given you. " +
                        $"Please respond with a short, friendly, and grateful message (1-2 sentences max). " +
                        $"Be creative and vary your responses. You can be playful, appreciative, or humorous. " +
                        $"If they've given many nachos (over 10), acknowledge their generosity. " +
                        $"Keep it casual and fun, like you're genuinely enjoying the nacho!";

            // Use the persona chat service to generate a contextual response
            // Using channelId as instanceId to maintain conversation context per channel
            var response = await _personaChatService.ChatAsync(channelId, userId, 0, prompt);

            // Fallback to default responses if AI fails
            if (string.IsNullOrWhiteSpace(response))
            {
                response = GetFallbackNachoResponse(totalNachos);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dynamic nacho response for user {UserId}", userId);
            // Return a fallback response if something goes wrong
            return GetFallbackNachoResponse(totalNachos);
        }
    }

    /// <summary>
    /// Gets a fallback nacho response when AI generation fails.
    /// </summary>
    private string GetFallbackNachoResponse(int totalNachos)
    {
        var responses = totalNachos switch
        {
            > 50 => new[]
            {
                "🌮 WOW! You're a nacho legend! Thanks for all the love!",
                "🌮 *drowning in nachos* You're the best nacho friend ever!",
                "🌮 My nacho hero! You've given me so many delicious treats!"
            },
            > 20 => new[]
            {
                "🌮 You're so generous! These nachos keep me going!",
                "🌮 *happy crunching* You really know how to spoil a bot!",
                "🌮 Another nacho from my favorite human! Thank you!"
            },
            > 10 => new[]
            {
                "🌮 Mmm, you always bring the best nachos! Thanks!",
                "🌮 *nom nom* Your nachos never disappoint!",
                "🌮 Getting quite the collection thanks to you!"
            },
            _ => new[]
            {
                "🌮 *Crunch crunch* Thanks for the nacho!",
                "🌮 Delicious! This nacho hits the spot!",
                "🌮 You're so kind! This nacho is perfect!",
                "🌮 *nom nom* Best nacho ever!",
                "🌮 Yummy! Thanks for thinking of me!"
            }
        };

        return responses[_random.Next(responses.Length)];
    }

    /// <inheritdoc/>
    public async Task<List<EmbedFieldBuilder>> GetNachoLeaderboardAsync(ulong serverId, int limit = 10)
    {
        var topGivers = await _userStatsRepository.GetTopNachoGiversAsync(serverId, limit);
        var fields = new List<EmbedFieldBuilder>();

        for (int i = 0; i < topGivers.Count; i++)
        {
            var user = topGivers[i];
            var medal = i switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"{i + 1}."
            };

            var nachosGiven = user.GetStat<int>("nachos_given", 0);
            fields.Add(new EmbedFieldBuilder()
                .WithName($"{medal} Rank {i + 1}")
                .WithValue($"<@{user.UserId}>\n🌮 {nachosGiven} nachos")
                .WithIsInline(true));
        }

        return fields;
    }

    /// <inheritdoc/>
    public async Task<int> GetTotalNachosAsync(ulong serverId)
    {
        return await _userStatsRepository.GetTotalNachosReceivedAsync(serverId);
    }

    /// <summary>
    /// Gets an available font with fallback options for cross-platform compatibility.
    /// </summary>
    private static Font? GetAvailableFont(float size, FontStyle style = FontStyle.Regular)
    {
        // List of font families to try, in order of preference
        var fontFamilies = new[]
        {
            "Arial",           // Windows
            "Liberation Sans", // Linux
            "DejaVu Sans",     // Linux/Unix
            "Helvetica",       // macOS
            "sans-serif"       // Generic fallback
        };

        foreach (var fontFamily in fontFamilies)
        {
            try
            {
                return SystemFonts.CreateFont(fontFamily, size, style);
            }
            catch (FontFamilyNotFoundException)
            {
                // Continue to next font
            }
        }

        // If all else fails, get any available font family
        var availableFamilies = SystemFonts.Families.ToArray();
        if (availableFamilies.Length > 0)
        {
            return SystemFonts.CreateFont(availableFamilies[0].Name, size, style);
        }

        // Return null if no fonts are available - caller should handle this
        return null;
    }

    /// <summary>
    /// Gets a contrasting text color for better readability.
    /// </summary>
    private static Color GetContrastColor(Rgba32 backgroundColor)
    {
        // Calculate luminance
        var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;

        // Return black for light backgrounds, white for dark backgrounds
        return luminance > 0.5 ? Color.Black : Color.White;
    }

    /// <inheritdoc/>
    public async Task<ColorPalette> GenerateColorTheoryPaletteAsync(ColorHarmonyType harmonyType, float? baseHue = null)
    {
        await Task.CompletedTask; // Make method async for future enhancements

        var hue = baseHue ?? _random.Next(0, 360);
        var palette = new ColorPalette
        {
            BaseHue = hue,
            HarmonyType = harmonyType,
            Name = GetPaletteName(harmonyType, hue),
            Description = GetPaletteDescription(harmonyType)
        };

        var colors = GenerateColorsByHarmony(harmonyType, hue);
        palette.Colors = colors;
        palette.Tags = GeneratePaletteTags(colors, harmonyType);

        return palette;
    }

    /// <inheritdoc/>
    public async Task<(Discord.MessageComponent components, IEnumerable<FileAttachment> attachments)> CreateInteractivePaletteAsync(ColorPalette palette, ulong userId)
    {
        // Generate color images for each color in the palette
        var colorImages = new List<FileAttachment>();
        var imageUrls = new List<string>();

        var imageGenerationTasks = palette.Colors.Select(async color =>
        {
            try
            {
                var colorImage = await GenerateColorImageAsync(color.Hex);
                var cleanHex = color.Hex.TrimStart('#').ToUpper();
                var fileName = $"palette_color_{cleanHex}.png";
                var imageUrl = $"attachment://{fileName}";

                var attachment = new FileAttachment(colorImage, fileName);
                return (success: true, attachment, imageUrl, color);
            }
            catch
            {
                return (success: false, attachment: (FileAttachment?)null, imageUrl: (string?)null, color);
            }
        });

        var results = await Task.WhenAll(imageGenerationTasks);

        foreach (var result in results)
        {
            if (result.success && result.attachment.HasValue && result.imageUrl != null)
            {
                colorImages.Add(result.attachment.Value);
                imageUrls.Add(result.imageUrl);
            }
        }

        // Create interactive ComponentsV2 display

        var components = new ComponentBuilderV2()
            .WithTextDisplay($"# 🎨 {palette.Name}\n## {palette.HarmonyType} Color Palette")
            .WithTextDisplay($"{palette.Description}\n\n**Base Hue:** {palette.BaseHue:F1}°")
            .WithMediaGallery(imageUrls.ToArray())
            .WithTextDisplay($"**Colors in this palette:**\n{string.Join("\n", palette.Colors.Select(c => $"• **{c.Name}** ({c.Hex.ToUpper()}) - {c.Role}"))}")
            .WithActionRow([
                new SelectMenuBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId("palette_color", palette.Id, userId.ToString()))
                    .WithPlaceholder("Select a color to view details...")
                    .WithOptions(palette.Colors.Select(color =>
                        new SelectMenuOptionBuilder()
                            .WithLabel($"{color.Name} {color.Hex.ToUpper()}")
                            .WithValue(color.Hex)
                            .WithDescription($"{color.Role} • HSL({color.Hue:F0}°, {color.Saturation:F0}%, {color.Lightness:F0}%)")
                            .WithEmote(Emoji.Parse(GetColorEmoji(color)))
                    ).ToList())
                    .WithMinValues(1)
                    .WithMaxValues(1)
            ])
            .WithActionRow([
                new ButtonBuilder()
                    .WithLabel("🔄 New Palette")
                    .WithCustomId(_componentHandler.GenerateCustomId("palette_regenerate", palette.HarmonyType.ToString(), userId.ToString()))
                    .WithStyle(ButtonStyle.Primary),
                new ButtonBuilder()
                    .WithLabel("🎲 Random Harmony")
                    .WithCustomId(_componentHandler.GenerateCustomId("palette_random", userId.ToString()))
                    .WithStyle(ButtonStyle.Secondary),
                new ButtonBuilder()
                    .WithLabel("💾 Export")
                    .WithCustomId(_componentHandler.GenerateCustomId("palette_export", palette.Id, userId.ToString()))
                    .WithStyle(ButtonStyle.Secondary)
            ])
            .Build();

        // Register component handlers
        RegisterPaletteHandlers(palette);

        return (components, colorImages);
    }

    private List<PaletteColor> GenerateColorsByHarmony(ColorHarmonyType harmonyType, float baseHue)
    {
        var colors = new List<PaletteColor>();
        var baseSaturation = 70f + _random.Next(-20, 21); // 50-90%
        var baseLightness = 50f + _random.Next(-15, 16);  // 35-65%

        switch (harmonyType)
        {
            case ColorHarmonyType.Monochromatic:
                colors.AddRange(GenerateMonochromaticColors(baseHue, baseSaturation));
                break;

            case ColorHarmonyType.Analogous:
                colors.AddRange(GenerateAnalogousColors(baseHue, baseSaturation, baseLightness));
                break;

            case ColorHarmonyType.Complementary:
                colors.AddRange(GenerateComplementaryColors(baseHue, baseSaturation, baseLightness));
                break;

            case ColorHarmonyType.SplitComplementary:
                colors.AddRange(GenerateSplitComplementaryColors(baseHue, baseSaturation, baseLightness));
                break;

            case ColorHarmonyType.Triadic:
                colors.AddRange(GenerateTriadicColors(baseHue, baseSaturation, baseLightness));
                break;

            case ColorHarmonyType.Tetradic:
                colors.AddRange(GenerateTetradicColors(baseHue, baseSaturation, baseLightness));
                break;

            case ColorHarmonyType.Square:
                colors.AddRange(GenerateSquareColors(baseHue, baseSaturation, baseLightness));
                break;
        }

        return colors;
    }

    private List<PaletteColor> GenerateMonochromaticColors(float baseHue, float baseSaturation)
    {
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation * 0.3f, 20f, "Deep Shadow", ColorRole.Neutral),
            CreatePaletteColor(baseHue, baseSaturation * 0.7f, 35f, "Dark Tone", ColorRole.Secondary),
            CreatePaletteColor(baseHue, baseSaturation, 50f, "Base Color", ColorRole.Primary),
            CreatePaletteColor(baseHue, baseSaturation * 0.6f, 70f, "Light Tone", ColorRole.Accent),
            CreatePaletteColor(baseHue, baseSaturation * 0.3f, 85f, "Soft Light", ColorRole.Highlight)
        };
    }

    private List<PaletteColor> GenerateAnalogousColors(float baseHue, float baseSaturation, float baseLightness)
    {
        return new List<PaletteColor>
        {
            CreatePaletteColor(NormalizeHue(baseHue - 30), baseSaturation * 0.8f, baseLightness * 0.8f, "Cool Neighbor", ColorRole.Secondary),
            CreatePaletteColor(NormalizeHue(baseHue - 15), baseSaturation * 0.9f, baseLightness * 0.9f, "Close Cool", ColorRole.Accent),
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Base Color", ColorRole.Primary),
            CreatePaletteColor(NormalizeHue(baseHue + 15), baseSaturation * 0.9f, baseLightness * 0.9f, "Close Warm", ColorRole.Accent),
            CreatePaletteColor(NormalizeHue(baseHue + 30), baseSaturation * 0.8f, baseLightness * 0.8f, "Warm Neighbor", ColorRole.Secondary)
        };
    }

    private List<PaletteColor> GenerateComplementaryColors(float baseHue, float baseSaturation, float baseLightness)
    {
        var complementHue = NormalizeHue(baseHue + 180);
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation, baseLightness * 0.7f, "Primary Dark", ColorRole.Primary),
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Primary", ColorRole.Primary),
            CreatePaletteColor(baseHue, baseSaturation * 0.4f, baseLightness * 1.4f, "Primary Light", ColorRole.Highlight),
            CreatePaletteColor(complementHue, baseSaturation, baseLightness, "Complement", ColorRole.Secondary),
            CreatePaletteColor(complementHue, baseSaturation * 0.4f, baseLightness * 1.4f, "Complement Light", ColorRole.Accent)
        };
    }

    private List<PaletteColor> GenerateSplitComplementaryColors(float baseHue, float baseSaturation, float baseLightness)
    {
        var split1 = NormalizeHue(baseHue + 150);
        var split2 = NormalizeHue(baseHue + 210);
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Base Color", ColorRole.Primary),
            CreatePaletteColor(baseHue, baseSaturation * 0.5f, baseLightness * 1.3f, "Base Light", ColorRole.Highlight),
            CreatePaletteColor(split1, baseSaturation * 0.8f, baseLightness * 0.9f, "Split One", ColorRole.Secondary),
            CreatePaletteColor(split2, baseSaturation * 0.8f, baseLightness * 0.9f, "Split Two", ColorRole.Secondary),
            CreatePaletteColor(NormalizeHue((split1 + split2) / 2), baseSaturation * 0.4f, baseLightness * 1.2f, "Split Blend", ColorRole.Accent)
        };
    }

    private List<PaletteColor> GenerateTriadicColors(float baseHue, float baseSaturation, float baseLightness)
    {
        var triad1 = NormalizeHue(baseHue + 120);
        var triad2 = NormalizeHue(baseHue + 240);
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Primary", ColorRole.Primary),
            CreatePaletteColor(triad1, baseSaturation * 0.8f, baseLightness * 0.9f, "Triad One", ColorRole.Secondary),
            CreatePaletteColor(triad2, baseSaturation * 0.8f, baseLightness * 0.9f, "Triad Two", ColorRole.Secondary),
            CreatePaletteColor(baseHue, baseSaturation * 0.3f, baseLightness * 1.4f, "Primary Light", ColorRole.Highlight),
            CreatePaletteColor(triad1, baseSaturation * 0.3f, baseLightness * 1.3f, "Triad Light", ColorRole.Accent)
        };
    }

    private List<PaletteColor> GenerateTetradicColors(float baseHue, float baseSaturation, float baseLightness)
    {
        var complement = NormalizeHue(baseHue + 180);
        var side1 = NormalizeHue(baseHue + 90);
        var side2 = NormalizeHue(baseHue + 270);
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Primary", ColorRole.Primary),
            CreatePaletteColor(complement, baseSaturation * 0.9f, baseLightness * 0.9f, "Complement", ColorRole.Secondary),
            CreatePaletteColor(side1, baseSaturation * 0.7f, baseLightness * 0.8f, "Side One", ColorRole.Accent),
            CreatePaletteColor(side2, baseSaturation * 0.7f, baseLightness * 0.8f, "Side Two", ColorRole.Accent),
            CreatePaletteColor(baseHue, baseSaturation * 0.3f, baseLightness * 1.4f, "Primary Light", ColorRole.Highlight)
        };
    }

    private List<PaletteColor> GenerateSquareColors(float baseHue, float baseSaturation, float baseLightness)
    {
        return new List<PaletteColor>
        {
            CreatePaletteColor(baseHue, baseSaturation, baseLightness, "Primary", ColorRole.Primary),
            CreatePaletteColor(NormalizeHue(baseHue + 90), baseSaturation * 0.8f, baseLightness * 0.9f, "Square One", ColorRole.Secondary),
            CreatePaletteColor(NormalizeHue(baseHue + 180), baseSaturation * 0.9f, baseLightness, "Square Two", ColorRole.Secondary),
            CreatePaletteColor(NormalizeHue(baseHue + 270), baseSaturation * 0.8f, baseLightness * 0.9f, "Square Three", ColorRole.Secondary),
            CreatePaletteColor(baseHue, baseSaturation * 0.3f, baseLightness * 1.4f, "Primary Light", ColorRole.Highlight)
        };
    }

    private PaletteColor CreatePaletteColor(float hue, float saturation, float lightness, string name, ColorRole role)
    {
        // Clamp values to valid ranges
        hue = NormalizeHue(hue);
        saturation = Math.Clamp(saturation, 0, 100);
        lightness = Math.Clamp(lightness, 0, 100);

        // Convert HSL to RGB
        var (r, g, b) = HslToRgb(hue, saturation, lightness);
        var hex = $"#{r:X2}{g:X2}{b:X2}";

        return new PaletteColor
        {
            Hex = hex,
            Name = name,
            Hue = hue,
            Saturation = saturation,
            Lightness = lightness,
            Role = role,
            Description = GetColorDescription(hue, saturation, lightness, role)
        };
    }

    private static float NormalizeHue(float hue)
    {
        while (hue < 0) hue += 360;
        while (hue >= 360) hue -= 360;
        return hue;
    }

    private static (byte r, byte g, byte b) HslToRgb(float h, float s, float l)
    {
        h /= 360f;
        s /= 100f;
        l /= 100f;

        float r, g, b;

        if (s == 0)
        {
            r = g = b = l; // Achromatic
        }
        else
        {
            float hue2rgb(float p, float q, float t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1f / 6) return p + (q - p) * 6 * t;
                if (t < 1f / 2) return q;
                if (t < 2f / 3) return p + (q - p) * (2f / 3 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;

            r = hue2rgb(p, q, h + 1f / 3);
            g = hue2rgb(p, q, h);
            b = hue2rgb(p, q, h - 1f / 3);
        }

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static string GetPaletteName(ColorHarmonyType harmonyType, float baseHue)
    {
        var hueNames = new[]
        {
            "Red", "Orange", "Yellow", "Lime", "Green", "Mint",
            "Cyan", "Azure", "Blue", "Purple", "Magenta", "Rose"
        };

        var hueIndex = (int)(baseHue / 30) % hueNames.Length;
        var hueName = hueNames[hueIndex];

        return $"{hueName} {harmonyType}";
    }

    private static string GetPaletteDescription(ColorHarmonyType harmonyType) => harmonyType switch
    {
        ColorHarmonyType.Monochromatic => "Single hue with varying saturation and lightness for elegant simplicity.",
        ColorHarmonyType.Analogous => "Adjacent hues on the color wheel for harmonious, natural-feeling combinations.",
        ColorHarmonyType.Complementary => "Opposite hues for maximum contrast and visual impact.",
        ColorHarmonyType.SplitComplementary => "Base hue with two colors adjacent to its complement for softer contrast.",
        ColorHarmonyType.Triadic => "Three evenly-spaced hues for vibrant, balanced color schemes.",
        ColorHarmonyType.Tetradic => "Four hues in two complementary pairs for rich, complex palettes.",
        ColorHarmonyType.Square => "Four evenly-spaced hues for bold, energetic color combinations.",
        _ => "A carefully crafted color harmony based on color theory principles."
    };

    private static List<string> GeneratePaletteTags(List<PaletteColor> colors, ColorHarmonyType harmonyType)
    {
        var tags = new List<string>();

        // Add harmony-based tags
        switch (harmonyType)
        {
            case ColorHarmonyType.Monochromatic:
                tags.AddRange(new[] { "Minimal", "Elegant", "Sophisticated", "Subtle" });
                break;
            case ColorHarmonyType.Complementary:
                tags.AddRange(new[] { "Bold", "Dramatic", "High-contrast", "Eye-catching" });
                break;
            case ColorHarmonyType.Analogous:
                tags.AddRange(new[] { "Natural", "Harmonious", "Calming", "Organic" });
                break;
            default:
                tags.AddRange(new[] { "Vibrant", "Dynamic", "Creative", "Modern" });
                break;
        }

        // Add lightness-based tags
        var avgLightness = colors.Average(c => c.Lightness);
        if (avgLightness > 70) tags.Add("Light");
        else if (avgLightness < 30) tags.Add("Dark");
        else tags.Add("Medium");

        // Add saturation-based tags
        var avgSaturation = colors.Average(c => c.Saturation);
        if (avgSaturation > 70) tags.Add("Vivid");
        else if (avgSaturation < 30) tags.Add("Muted");

        return tags.Take(6).ToList();
    }

    private static string GetColorEmoji(PaletteColor color) => color.Role switch
    {
        ColorRole.Primary => "🎯",
        ColorRole.Secondary => "🔹",
        ColorRole.Accent => "✨",
        ColorRole.Neutral => "⚪",
        ColorRole.Highlight => "💫",
        _ => "🎨"
    };

    private static string GetColorDescription(float hue, float saturation, float lightness, ColorRole role)
    {
        var intensity = saturation > 70 ? "vibrant" : saturation > 40 ? "moderate" : "subtle";
        var brightness = lightness > 70 ? "light" : lightness > 30 ? "medium" : "dark";
        var roleDesc = role.ToString().ToLower();

        return $"A {brightness}, {intensity} {roleDesc} color";
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, Stream>> GeneratePaletteColorImagesAsync(ColorPalette palette)
    {
        var imageStreams = new Dictionary<string, Stream>();

        try
        {
            foreach (var color in palette.Colors)
            {
                try
                {
                    var stream = await GenerateEnhancedColorImageAsync(color);
                    imageStreams[color.Hex] = stream;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate image for color {ColorHex}", color.Hex);
                    // Create a fallback simple color image
                    try
                    {
                        var fallbackStream = await GenerateColorImageAsync(color.Hex);
                        imageStreams[color.Hex] = fallbackStream;
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogError(fallbackEx, "Failed to generate fallback image for color {ColorHex}", color.Hex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating palette color images for palette {PaletteId}", palette.Id);
        }

        return imageStreams;
    }

    private async Task<Stream> GenerateEnhancedColorImageAsync(PaletteColor paletteColor)
    {
        try
        {
            const int width = 300;
            const int height = 200;
            const int margin = 20;

            using var image = new Image<Rgba32>(width, height);

            // Parse the color
            if (!Rgba32.TryParseHex(paletteColor.Hex, out var mainColor))
            {
                throw new ArgumentException($"Invalid hex color: {paletteColor.Hex}");
            }

            // Create gradient background
            var lightColor = CreateColorVariant(mainColor, 1.3f, 1.2f);
            var darkColor = CreateColorVariant(mainColor, 0.7f, 0.8f);

            image.Mutate(ctx =>
            {
                // Fill with gradient
                var gradientBrush = new LinearGradientBrush(
                    new PointF(0, 0), new PointF(width, height),
                    GradientRepetitionMode.None,
                    new ColorStop(0f, lightColor),
                    new ColorStop(0.5f, mainColor),
                    new ColorStop(1f, darkColor));

                ctx.Fill(gradientBrush);

                // Add decorative elements based on color role
                AddRoleBasedDecorations(ctx, paletteColor.Role, width, height, mainColor);
            });

            // Add text information
            var font = GetAvailableFont(14, FontStyle.Bold);
            if (font != null)
            {
                var contrastColor = GetContrastColor(mainColor);

                image.Mutate(ctx =>
                {
                    // Color name
                    var nameOptions = new RichTextOptions(font)
                    {
                        Origin = new PointF(margin, margin),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    ctx.DrawText(nameOptions, paletteColor.Name, contrastColor);

                    // Hex code
                    var hexFont = GetAvailableFont(12, FontStyle.Regular);
                    if (hexFont != null)
                    {
                        var hexOptions = new RichTextOptions(hexFont)
                        {
                            Origin = new PointF(margin, margin + 25),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        ctx.DrawText(hexOptions, paletteColor.Hex.ToUpper(), contrastColor);

                        // HSL values
                        var hslOptions = new RichTextOptions(hexFont)
                        {
                            Origin = new PointF(margin, margin + 45),
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        var hslText = $"HSL({paletteColor.Hue:F0}°, {paletteColor.Saturation:F0}%, {paletteColor.Lightness:F0}%)";
                        ctx.DrawText(hslOptions, hslText, contrastColor);

                        // Role
                        var roleOptions = new RichTextOptions(hexFont)
                        {
                            Origin = new PointF(width - margin, height - margin - 15),
                            HorizontalAlignment = HorizontalAlignment.Right
                        };
                        ctx.DrawText(roleOptions, paletteColor.Role.ToString(), contrastColor);
                    }
                });
            }

            // Convert to stream
            var stream = new MemoryStream();
            await image.SaveAsPngAsync(stream);
            stream.Position = 0;

            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enhanced color image for {ColorHex}", paletteColor.Hex);
            throw;
        }
    }

    private static Rgba32 CreateColorVariant(Rgba32 baseColor, float saturationMultiplier, float lightnessMultiplier)
    {
        // Convert RGB to HSL
        var (h, s, l) = RgbToHsl(baseColor.R, baseColor.G, baseColor.B);

        // Adjust saturation and lightness
        s = Math.Clamp(s * saturationMultiplier, 0, 100);
        l = Math.Clamp(l * lightnessMultiplier, 0, 100);

        // Convert back to RGB
        var (r, g, b) = HslToRgb(h, s, l);
        return new Rgba32(r, g, b);
    }

    private static (float h, float s, float l) RgbToHsl(byte red, byte green, byte blue)
    {
        float r = red / 255f;
        float g = green / 255f;
        float b = blue / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));

        float h, s, l = (max + min) / 2;

        if (max == min)
        {
            h = s = 0; // Achromatic
        }
        else
        {
            float d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

            h = max switch
            {
                _ when max == r => (g - b) / d + (g < b ? 6 : 0),
                _ when max == g => (b - r) / d + 2,
                _ => (r - g) / d + 4,
            };

            h /= 6;
        }

        return (h * 360, s * 100, l * 100);
    }

    private static void AddRoleBasedDecorations(IImageProcessingContext ctx, ColorRole role, int width, int height, Rgba32 color)
    {
        var contrastColor = GetContrastColor(color);
        var contrastRgba = contrastColor.ToPixel<Rgba32>();
        var decorationColor = Color.FromRgba(contrastRgba.R, contrastRgba.G, contrastRgba.B, 100);

        switch (role)
        {
            case ColorRole.Primary:
                // Add a bold border
                ctx.Draw(contrastColor, 3f, new RectangleF(5, 5, width - 10, height - 10));
                break;

            case ColorRole.Secondary:
                // Add corner triangles
                var trianglePoints = new PointF[]
                {
                    new(width - 30, 10),
                    new(width - 10, 10),
                    new(width - 10, 30)
                };
                ctx.Fill(decorationColor, new Polygon(new LinearLineSegment(trianglePoints)));
                break;

            case ColorRole.Accent:
                // Add subtle dots pattern
                for (int x = 20; x < width - 20; x += 25)
                {
                    for (int y = 20; y < height - 20; y += 25)
                    {
                        ctx.Fill(decorationColor, new EllipsePolygon(x, y, 2));
                    }
                }
                break;

            case ColorRole.Highlight:
                // Add diagonal lines
                for (int i = 0; i < width + height; i += 15)
                {
                    var line = new LinearLineSegment(new PointF(i, 0), new PointF(i - height, height));
                    ctx.Draw(decorationColor, 1f, new SixLabors.ImageSharp.Drawing.Path(line));
                }
                break;

            case ColorRole.Neutral:
                // Add simple corner squares
                ctx.Fill(decorationColor, new RectangleF(10, 10, 15, 15));
                ctx.Fill(decorationColor, new RectangleF(width - 25, 10, 15, 15));
                ctx.Fill(decorationColor, new RectangleF(10, height - 25, 15, 15));
                ctx.Fill(decorationColor, new RectangleF(width - 25, height - 25, 15, 15));
                break;
        }
    }

    private void RegisterColorHandlers()
    {
        // Register handlers for color-related interactions
        _componentHandler.RegisterHandler("random_color", HandleRandomColorAsync);
        _componentHandler.RegisterHandler("generate_palette", HandleGeneratePaletteAsync);
        _componentHandler.RegisterHandler("palette_color", HandlePaletteColorAsync);
        _componentHandler.RegisterHandler("palette_regenerate", HandlePaletteRegenerateAsync);
        _componentHandler.RegisterHandler("palette_random", HandlePaletteRandomAsync);
        _componentHandler.RegisterHandler("palette_export", HandlePaletteExportAsync);

        _logger.LogDebug("Registered color component handlers");
    }

    private async Task<bool> HandleRandomColorAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        try
        {
            // Generate a random color
            var randomHex = GenerateRandomColor();

            using var colorImage = await GenerateColorImageAsync(randomHex);
            var imageUrl = $"attachment://color_{randomHex.TrimStart('#')}.png";

            // Parse color for additional information
            var colorValue = uint.Parse(randomHex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
            var r = (colorValue >> 16) & 255;
            var g = (colorValue >> 8) & 255;
            var b = colorValue & 255;

            // Convert RGB to HSL for additional information
            var (h, s, l) = RgbToHsl((byte)r, (byte)g, (byte)b);

            var attachment = new Discord.FileAttachment(colorImage, $"color_{randomHex.TrimStart('#')}.png");

            // Create ComponentsV2 display with color information
            var components = new Discord.ComponentBuilderV2()
                .WithTextDisplay($"# 🎲 Random Color\n## {randomHex.ToUpper()}")
                .WithTextDisplay($"**Hex:** {randomHex.ToUpper()}\n**RGB:** {r}, {g}, {b}\n**HSL:** {h:F0}°, {s:F0}%, {l:F0}%")
                .WithMediaGallery([imageUrl])
                .WithActionRow([
                    new Discord.ButtonBuilder()
                        .WithLabel("🎲 Another Random")
                        .WithCustomId("random_color")
                        .WithStyle(Discord.ButtonStyle.Primary),
                    new Discord.ButtonBuilder()
                        .WithLabel("🎨 Generate Palette")
                        .WithCustomId($"generate_palette:{randomHex.TrimStart('#')}")
                        .WithStyle(Discord.ButtonStyle.Secondary)
                ])
                .Build();

            var attachmentList = new List<FileAttachment> { attachment };

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = Discord.MessageFlags.ComponentsV2;
                msg.Attachments = attachmentList;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling random color interaction");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to generate random color. Please try again.");
            return false;
        }
    }

    private async Task<bool> HandleGeneratePaletteAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        try
        {
            // Extract hex color from custom ID
            var hexColor = context.Parameters.Length > 0 ? context.Parameters[0] : null;

            float? baseHue = null;
            if (!string.IsNullOrEmpty(hexColor))
            {
                var colorValue = uint.Parse(hexColor, System.Globalization.NumberStyles.HexNumber);
                var r = (colorValue >> 16) & 255;
                var g = (colorValue >> 8) & 255;
                var b = colorValue & 255;
                var (h, _, _) = RgbToHsl((byte)r, (byte)g, (byte)b);
                baseHue = h;
            }

            // Generate a random harmony type
            var harmonyType = (ColorHarmonyType)Random.Shared.Next(0, 7);

            var palette = await GenerateColorTheoryPaletteAsync(harmonyType, baseHue);

            // Create interactive palette with ComponentsV2
            var (components, attachments) = await CreateInteractivePaletteAsync(palette, component.User.Id);

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = Discord.MessageFlags.ComponentsV2;
                msg.Attachments = attachments.ToArray();
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling generate palette interaction");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to generate color palette. Please try again.");
            return false;
        }
    }

    private async Task<bool> HandlePaletteColorAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        // Handle palette color selection from dropdown
        try
        {
            var selectedValues = component.Data.Values;
            if (selectedValues?.Any() != true)
                return false;

            var selectedColor = selectedValues.First();

            using var colorImage = await GenerateColorImageAsync(selectedColor);
            var imageUrl = $"attachment://selected_color_{selectedColor.TrimStart('#')}.png";

            var attachment = new Discord.FileAttachment(colorImage, $"selected_color_{selectedColor.TrimStart('#')}.png");

            var components = new Discord.ComponentBuilderV2()
                .WithTextDisplay($"# 🎯 Selected Color\n## {selectedColor.ToUpper()}")
                .WithTextDisplay("This color has been selected from the palette.")
                .WithMediaGallery([imageUrl])
                .Build();

            var attachmentList = new List<FileAttachment> { attachment };

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = Discord.MessageFlags.ComponentsV2;
                msg.Attachments = attachmentList;
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling palette color selection");
            return false;
        }
    }

    private async Task<bool> HandlePaletteRegenerateAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        try
        {
            var harmonyTypeStr = context.Parameters.Length > 0 ? context.Parameters[0] : null;
            var userIdStr = context.Parameters.Length > 1 ? context.Parameters[1] : null;

            if (!Enum.TryParse<ColorHarmonyType>(harmonyTypeStr, out var harmonyType))
                harmonyType = (ColorHarmonyType)Random.Shared.Next(0, 7);

            if (!ulong.TryParse(userIdStr, out var userId))
                userId = component.User.Id;

            var palette = await GenerateColorTheoryPaletteAsync(harmonyType);
            var (components, attachments) = await CreateInteractivePaletteAsync(palette, userId);

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = Discord.MessageFlags.ComponentsV2;
                msg.Attachments = attachments.ToArray();
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling palette regeneration");
            return false;
        }
    }

    private async Task<bool> HandlePaletteRandomAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        try
        {
            var userIdStr = context.Parameters.Length > 0 ? context.Parameters[0] : null;

            if (!ulong.TryParse(userIdStr, out var userId))
                userId = component.User.Id;

            var harmonyType = (ColorHarmonyType)Random.Shared.Next(0, 7);
            var palette = await GenerateColorTheoryPaletteAsync(harmonyType);
            var (components, attachments) = await CreateInteractivePaletteAsync(palette, userId);

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = Discord.MessageFlags.ComponentsV2;
                msg.Attachments = attachments.ToArray();
                msg.Embed = null;
                msg.Content = null;
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling random palette");
            return false;
        }
    }

    private async Task<bool> HandlePaletteExportAsync(Discord.WebSocket.SocketMessageComponent component, ComponentHandler.ComponentContext context)
    {
        try
        {
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "🚧 Palette export feature is coming soon!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling palette export");
            return false;
        }
    }

    private string GenerateRandomColor()
    {
        var r = _random.Next(0, 256);
        var g = _random.Next(0, 256);
        var b = _random.Next(0, 256);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private void RegisterPaletteHandlers(ColorPalette palette)
    {
        // Palette handlers are now registered globally in RegisterColorHandlers()
        _logger.LogDebug("Palette interaction handlers are registered globally for palette {PaletteId}", palette.Id);
    }
}