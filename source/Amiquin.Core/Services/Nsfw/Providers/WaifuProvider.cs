using Amiquin.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Amiquin.Core.Services.Nsfw.Providers;

/// <summary>
/// Provider for waifu.im API
/// </summary>
public class WaifuProvider : INsfwProvider
{
    private readonly ILogger<WaifuProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    private const string BaseUrl = "https://api.waifu.im";
    private const string ApiVersion = "v5";
    private static readonly string[] NsfwTags = { "oppai", "ass", "hentai", "milf", "oral", "paizuri", "ecchi" };

    // Circuit breaker for rate limiting
    private static DateTime _lastRateLimitHit = DateTime.MinValue;
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(10);
    private static int _consecutiveRateLimits = 0;

    public string Name => "waifu.im";

    public bool IsAvailable
    {
        get
        {
            var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;
            return DateTime.UtcNow - _lastRateLimitHit >= effectiveCooldown;
        }
    }

    public WaifuProvider(ILogger<WaifuProvider> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<NsfwImage?> FetchImageAsync()
    {
        if (!IsAvailable)
        {
            _logger.LogDebug("Waifu provider is currently rate-limited");
            return null;
        }

        var tag = NsfwTags[_random.Next(NsfwTags.Length)];

        try
        {
            var url = $"{BaseUrl}/search?included_tags={tag}&is_nsfw=true";
            _logger.LogInformation("Making request to {Provider}: {Url}", Name, url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept-Version", ApiVersion);

            await Task.Delay(200); // Rate limiting delay

            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                HandleRateLimit();
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} response from {Provider}",
                    response.StatusCode, Name);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);

            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesElement) &&
                imagesElement.GetArrayLength() > 0)
            {
                _consecutiveRateLimits = 0; // Reset on success

                var firstImage = imagesElement[0];
                return new NsfwImage
                {
                    Url = firstImage.GetProperty("url").GetString() ?? string.Empty,
                    Source = Name,
                    Tags = tag,
                    Width = firstImage.TryGetProperty("width", out var width) ? width.GetInt32() : null,
                    Height = firstImage.TryGetProperty("height", out var height) ? height.GetInt32() : null,
                    Artist = firstImage.TryGetProperty("artist", out var artist) &&
                             artist.ValueKind != JsonValueKind.Null &&
                             artist.TryGetProperty("name", out var artistName)
                             ? artistName.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from {Provider}", Name);
        }

        return null;
    }

    public async Task<List<NsfwImage>> FetchImagesAsync(int count)
    {
        var images = new List<NsfwImage>();

        for (int i = 0; i < count; i++)
        {
            var image = await FetchImageAsync();
            if (image != null)
            {
                images.Add(image);
            }

            // Add delay between requests to respect rate limits
            if (i < count - 1)
            {
                await Task.Delay(300);
            }
        }

        return images;
    }

    /// <summary>
    /// Handles rate limiting for the provider
    /// </summary>
    private void HandleRateLimit()
    {
        _lastRateLimitHit = DateTime.UtcNow;
        _consecutiveRateLimits++;

        var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;

        _logger.LogWarning("Rate limited by {Provider}. Cooldown activated for {Cooldown} seconds (consecutive: {Count})",
            Name, effectiveCooldown.TotalSeconds, _consecutiveRateLimits);
    }
}