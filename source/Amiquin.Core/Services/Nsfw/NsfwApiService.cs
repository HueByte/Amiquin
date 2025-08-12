using Amiquin.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace Amiquin.Core.Services.Nsfw;

/// <summary>
/// Represents the result of an NSFW API operation with status information.
/// </summary>
public class NsfwApiResult
{
    public List<NsfwImage> Images { get; set; } = new();
    public bool IsSuccess => Images.Count > 0;
    public string? ErrorMessage { get; set; }
    public bool IsRateLimited { get; set; }
    public bool IsTemporaryFailure { get; set; }
    public DateTime? RetryAfter { get; set; }
}

public class NsfwApiService : INsfwApiService
{
    private readonly ILogger<NsfwApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly EHentaiService _eHentaiService;
    private readonly Random _random = new();

    // Circuit breaker for rate limiting
    private static DateTime _lastRateLimitHit = DateTime.MinValue;
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(10);
    private static int _consecutiveRateLimits = 0;

    private const int MaxRetryAttempts = 3;
    private const int MaxFetchAttempts = 10;

    // Provider configurations
    private readonly Dictionary<string, ProviderConfig> _providers = new()
    {
        {
            "waifu", new ProviderConfig
            {
                Name = "waifu.im",
                BaseUrl = "https://api.waifu.im",
                ApiVersion = "v5",
                Tags = new[] { "waifu", "maid", "uniform", "kitsune", "elf" },
                NsfwTags = new[] { "oppai", "ass", "hentai", "milf", "oral", "paizuri", "ecchi" },
                RateLimited = false
            }
        },
        {
            "purrbot", new ProviderConfig
            {
                Name = "api.purrbot.site",
                BaseUrl = "https://api.purrbot.site/v2",
                Endpoints = new[] { "anal/gif", "blowjob/gif", "cum/gif", "fuck/gif", "pussylick/gif", "solo/gif", "threesome_fff/gif", "threesome_ffm/gif", "threesome_mmf/gif", "yuri/gif", "neko/img", "neko/gif" },
                RateLimited = false
            }
        },
        {
            "ehentai", new ProviderConfig
            {
                Name = "e-hentai.org",
                BaseUrl = "https://e-hentai.org",
                RateLimited = false
            }
        }
    };

    public NsfwApiService(ILogger<NsfwApiService> logger, HttpClient httpClient, EHentaiService eHentaiService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _eHentaiService = eHentaiService;

        _logger.LogInformation("NSFW API service initialized with providers: {Providers}",
            string.Join(", ", _providers.Keys));
    }

    public async Task<List<NsfwImage>> GetDailyNsfwImagesAsync(int waifuCount = 5, int otherCount = 5)
    {
        return await GetNsfwImagesAsync(waifuCount + otherCount);
    }

    public async Task<NsfwApiResult> GetNsfwImagesWithStatusAsync(int waifuCount = 2, int otherCount = 8)
    {
        var result = new NsfwApiResult();

        // Check if we're currently rate-limited
        var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;
        var isCurrentlyRateLimited = DateTime.UtcNow - _lastRateLimitHit < effectiveCooldown;

        if (isCurrentlyRateLimited)
        {
            result.IsRateLimited = true;
            result.IsTemporaryFailure = true;
            result.ErrorMessage = "NSFW services are currently rate-limited. Please try again later.";
            result.RetryAfter = _lastRateLimitHit.Add(effectiveCooldown);
            return result;
        }

        try
        {
            var images = await GetNsfwImagesAsync(waifuCount + otherCount);

            if (images.Count == 0)
            {
                result.ErrorMessage = "NSFW image services are currently experiencing issues. Please try again later.";
                result.IsTemporaryFailure = true;
            }
            else if (images.Count < (waifuCount + otherCount) / 2)
            {
                result.Images.AddRange(images);
                result.ErrorMessage = "Some NSFW services are having issues, showing partial results.";
                result.IsTemporaryFailure = true;
            }
            else
            {
                result.Images.AddRange(images);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetNsfwImagesWithStatusAsync");
            result.ErrorMessage = "NSFW image services are temporarily unavailable. Please try again later.";
            result.IsTemporaryFailure = true;
        }

        return result;
    }

    public async Task<List<NsfwImage>> GetWaifuImagesAsync(int count = 5)
    {
        return await GetNsfwImagesAsync(count, new[] { "waifu" });
    }

    public async Task<List<NsfwImage>> GetAlternativeNsfwImagesAsync(int count = 5)
    {
        return await GetNsfwImagesAsync(count);
    }

    /// <summary>
    /// Gets NSFW images from available providers
    /// </summary>
    /// <param name="count">Number of images to fetch</param>
    /// <param name="preferredProviders">Optional list of preferred providers to use</param>
    /// <returns>List of NSFW images</returns>
    private async Task<List<NsfwImage>> GetNsfwImagesAsync(int count = 5, string[]? preferredProviders = null)
    {
        var results = new ConcurrentBag<NsfwImage>();

        try
        {
            // Get available providers
            var availableProviders = GetAvailableProviders(preferredProviders);

            if (availableProviders.Count == 0)
            {
                _logger.LogWarning("No available providers for NSFW images");
                return new List<NsfwImage>();
            }

            // Randomize and select providers for the requested count
            var selectedProviders = availableProviders
                .OrderBy(x => _random.Next())
                .Take(count)
                .ToList();

            _logger.LogInformation("Selected {Count} randomized NSFW providers: {Providers}",
                selectedProviders.Count, string.Join(", ", selectedProviders));

            // Execute provider calls in parallel
            var tasks = selectedProviders.Select(provider => Task.Run(async () =>
            {
                try
                {
                    var image = await FetchFromProviderAsync(provider);
                    if (image != null && !string.IsNullOrEmpty(image.Url))
                    {
                        results.Add(image);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Provider {Provider} failed to fetch image", provider);
                }
            }));

            await Task.WhenAll(tasks);

            _logger.LogInformation("Fetched {ActualCount} out of {RequestedCount} NSFW images from {ProviderCount} providers",
                results.Count, count, selectedProviders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching NSFW images");
        }

        // Remove duplicates and return up to count images
        return results
            .GroupBy(img => img.Url)
            .Select(group => group.First())
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Gets list of available providers, filtering out rate-limited ones
    /// </summary>
    private List<string> GetAvailableProviders(string[]? preferredProviders = null)
    {
        var providers = preferredProviders?.ToList() ?? _providers.Keys.ToList();

        // Filter out rate-limited providers
        var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;
        var isWaifuRateLimited = DateTime.UtcNow - _lastRateLimitHit < effectiveCooldown;

        if (isWaifuRateLimited && providers.Contains("waifu"))
        {
            providers.Remove("waifu");
            _logger.LogDebug("Excluding waifu.im provider due to rate limiting");
        }

        return providers;
    }

    /// <summary>
    /// Fetches an image from a specific provider
    /// </summary>
    private async Task<NsfwImage?> FetchFromProviderAsync(string providerName)
    {
        _logger.LogDebug("Calling NSFW provider: {Provider}", providerName);

        return providerName switch
        {
            "waifu" => await FetchFromWaifuAsync(),
            "purrbot" => await FetchFromPurrbotAsync(),
            "ehentai" => await FetchFromEHentaiAsync(),
            _ => null
        };
    }

    /// <summary>
    /// Fetches image from waifu.im API
    /// </summary>
    private async Task<NsfwImage?> FetchFromWaifuAsync()
    {
        var config = _providers["waifu"];
        var tag = config.NsfwTags![_random.Next(config.NsfwTags.Length)];

        try
        {
            var url = $"{config.BaseUrl}/search?included_tags={tag}&is_nsfw=true";
            _logger.LogInformation("Making request to {Provider}: {Url}", config.Name, url);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept-Version", config.ApiVersion);

            await Task.Delay(200); // Rate limiting delay

            using var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                HandleRateLimit(config.Name);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} response from {Provider}",
                    response.StatusCode, config.Name);
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
                    Source = config.Name,
                    Tags = tag,
                    Width = firstImage.TryGetProperty("width", out var width) ? width.GetInt32() : null,
                    Height = firstImage.TryGetProperty("height", out var height) ? height.GetInt32() : null,
                    Artist = firstImage.TryGetProperty("artist", out var artist) &&
                             artist.TryGetProperty("name", out var artistName)
                             ? artistName.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from {Provider}", config.Name);
        }

        return null;
    }

    /// <summary>
    /// Fetches image from purrbot API
    /// </summary>
    private async Task<NsfwImage?> FetchFromPurrbotAsync()
    {
        var config = _providers["purrbot"];
        var endpoint = config.Endpoints![_random.Next(config.Endpoints.Length)];

        try
        {
            var url = $"{config.BaseUrl}/img/nsfw/{endpoint}";
            _logger.LogInformation("Making request to {Provider}: {Url}", config.Name, url);

            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("link", out var linkElement))
            {
                var imageUrl = linkElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new NsfwImage
                    {
                        Url = imageUrl,
                        Source = config.Name,
                        Tags = endpoint.Replace("/", "_")
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch image from {Provider} endpoint: {Endpoint}", config.Name, endpoint);
        }

        return null;
    }

    /// <summary>
    /// Fetches image from e-hentai by scraping gallery pages
    /// </summary>
    private async Task<NsfwImage?> FetchFromEHentaiAsync()
    {
        var config = _providers["ehentai"];

        try
        {
            _logger.LogInformation("Making request to {Provider} for direct image URL", config.Name);

            // Get direct image URLs from e-hentai galleries
            var imageUrls = await _eHentaiService.GetRandomImageUrlsAsync(1, "hentai");

            if (imageUrls.Count == 0)
            {
                _logger.LogWarning("No image URLs returned from {Provider}", config.Name);
                return null;
            }

            var imageUrl = imageUrls[0];

            return new NsfwImage
            {
                Url = imageUrl,
                Source = config.Name,
                Tags = "hentai",
                Artist = "e-hentai"
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch image from {Provider}", config.Name);
            return null;
        }
    }

    /// <summary>
    /// Handles rate limiting for providers
    /// </summary>
    private void HandleRateLimit(string providerName)
    {
        _lastRateLimitHit = DateTime.UtcNow;
        _consecutiveRateLimits++;

        var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;

        _logger.LogWarning("Rate limited by {Provider}. Cooldown activated for {Cooldown} seconds (consecutive: {Count})",
            providerName, effectiveCooldown.TotalSeconds, _consecutiveRateLimits);
    }
}

/// <summary>
/// Configuration for NSFW API providers
/// </summary>
public class ProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiVersion { get; set; }
    public string[]? Tags { get; set; }
    public string[]? NsfwTags { get; set; }
    public string[]? Endpoints { get; set; }
    public bool RateLimited { get; set; }
}