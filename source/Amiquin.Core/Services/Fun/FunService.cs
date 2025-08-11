using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Amiquin.Core.IRepositories;
using Discord;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Fun;

/// <summary>
/// Service implementation for handling fun commands and interactions.
/// </summary>
public class FunService : IFunService
{
    private readonly IUserStatsRepository _userStatsRepository;
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

    public FunService(
        IUserStatsRepository userStatsRepository, 
        ILogger<FunService> logger,
        HttpClient httpClient)
    {
        _userStatsRepository = userStatsRepository;
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
            var color = ColorTranslator.FromHtml($"#{hexColor}");
            
            // Create a 300x100 image with the color
            using var bitmap = new Bitmap(300, 100);
            using var graphics = Graphics.FromImage(bitmap);
            
            // Fill with the color
            graphics.Clear(color);
            
            // Add a border
            using var pen = new Pen(System.Drawing.Color.Black, 2);
            graphics.DrawRectangle(pen, 1, 1, bitmap.Width - 2, bitmap.Height - 2);
            
            // Add text with color info
            var textColor = GetContrastColor(color);
            using var font = new Font("Arial", 12, FontStyle.Bold);
            using var brush = new SolidBrush(textColor);
            
            var text = $"#{hexColor.ToUpper()}";
            var textSize = graphics.MeasureString(text, font);
            var x = (bitmap.Width - textSize.Width) / 2;
            var y = (bitmap.Height - textSize.Height) / 2;
            
            graphics.DrawString(text, font, brush, x, y);
            
            // Convert to stream
            var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
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
    public async Task<int> GiveNachoAsync(ulong userId, ulong serverId)
    {
        var userStats = await _userStatsRepository.GetOrCreateUserStatsAsync(userId, serverId);
        var newTotal = userStats.IncrementStat("nachos_given");
        await _userStatsRepository.UpdateUserStatsAsync(userStats);
        
        _logger.LogDebug("User {UserId} gave a nacho to Amiquin. Total: {Total}", userId, newTotal);
        return newTotal;
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
    /// Gets a contrasting text color for better readability.
    /// </summary>
    private static System.Drawing.Color GetContrastColor(System.Drawing.Color backgroundColor)
    {
        // Calculate luminance
        var luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
        
        // Return black for light backgrounds, white for dark backgrounds
        return luminance > 0.5 ? System.Drawing.Color.Black : System.Drawing.Color.White;
    }
}