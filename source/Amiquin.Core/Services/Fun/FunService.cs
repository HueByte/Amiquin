using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using Color = SixLabors.ImageSharp.Color;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Toggle;
using Discord;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Fun;

/// <summary>
/// Service implementation for handling fun commands and interactions.
/// </summary>
public class FunService : IFunService
{
    private readonly IUserStatsRepository _userStatsRepository;
    private readonly IToggleService _toggleService;
    private readonly IPersonaChatService _personaChatService;
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
        ILogger<FunService> logger,
        HttpClient httpClient)
    {
        _userStatsRepository = userStatsRepository;
        _toggleService = toggleService;
        _personaChatService = personaChatService;
        _logger = logger;
        _httpClient = httpClient;
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
}