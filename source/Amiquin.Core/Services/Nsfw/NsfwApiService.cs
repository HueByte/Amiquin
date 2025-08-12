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
    private readonly Random _random = new();

    // Circuit breaker for rate limiting
    private static DateTime _lastRateLimitHit = DateTime.MinValue;
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(10); // Increased cooldown
    private static int _consecutiveRateLimits = 0;

    private const int MaxRetryAttempts = 3;
    private const int MaxFetchAttempts = 10; // Prevent infinite loops

    // Hardcoded waifu.im API settings
    private const string WaifuApiBaseUrl = "https://api.waifu.im";
    private const string WaifuApiVersion = "v5";

    private readonly string[] _waifuTags = new[]
    {
        "waifu", "maid", "uniform", "kitsune", "elf"
    };

    private readonly string[] _nsfwTags = new[]
    {
        "oppai", "ass", "hentai", "milf", "oral", "paizuri", "ecchi"
    };

    public NsfwApiService(ILogger<NsfwApiService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;

        _logger.LogInformation("NSFW API service initialized with multiple providers including waifu.im (v{Version})", WaifuApiVersion);
    }

    public async Task<List<NsfwImage>> GetDailyNsfwImagesAsync(int waifuCount = 5, int otherCount = 5)
    {
        var images = new List<NsfwImage>();

        try
        {
            var waifuTask = GetWaifuImagesAsync(waifuCount);
            var alternativeTask = GetAlternativeNsfwImagesAsync(otherCount);

            await Task.WhenAll(waifuTask, alternativeTask);

            images.AddRange(await waifuTask);
            images.AddRange(await alternativeTask);

            // Shuffle the images for variety
            images = images.OrderBy(x => _random.Next()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily NSFW images");
        }

        return images;
    }

    public async Task<NsfwApiResult> GetNsfwImagesWithStatusAsync(int waifuCount = 2, int otherCount = 8)
    {
        var result = new NsfwApiResult();
        var isRateLimited = DateTime.UtcNow - _lastRateLimitHit < _rateLimitCooldown;

        if (isRateLimited)
        {
            result.IsRateLimited = true;
            result.IsTemporaryFailure = true;
            result.RetryAfter = _lastRateLimitHit.Add(_rateLimitCooldown);
            result.ErrorMessage = "NSFW image services are temporarily rate-limited. Please try again in a few minutes.";

            // Still try fallback services
            var fallbackImages = await GetAlternativeNsfwImagesAsync(waifuCount + otherCount);
            if (fallbackImages.Count > 0)
            {
                result.Images.AddRange(fallbackImages);
                result.ErrorMessage = "Primary NSFW services are rate-limited, showing results from backup sources.";
                result.IsTemporaryFailure = false;
            }

            return result;
        }

        try
        {
            var images = await GetDailyNsfwImagesAsync(waifuCount, otherCount);

            if (images.Count == 0)
            {
                result.ErrorMessage = "NSFW image services are currently experiencing issues. Please try again later.";
                result.IsTemporaryFailure = true;
            }
            else if (images.Count < (waifuCount + otherCount) / 2) // Less than half requested
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
        var images = new List<NsfwImage>();

        try
        {
            // Check circuit breaker - if we recently hit rate limits, skip waifu.im
            var effectiveCooldown = _consecutiveRateLimits > 3
                ? TimeSpan.FromMinutes(20)
                : _rateLimitCooldown;

            if (DateTime.UtcNow - _lastRateLimitHit < effectiveCooldown)
            {
                _logger.LogInformation("Skipping waifu.im due to recent rate limiting, cooldown until {CooldownEnd} (consecutive: {Count})",
                    _lastRateLimitHit.Add(effectiveCooldown), _consecutiveRateLimits);
                return images;
            }

            // Get random versatile tags for variety (these work with is_nsfw=true)
            var selectedTags = _waifuTags.OrderBy(x => _random.Next()).Take(count).ToList();

            var tasks = selectedTags.Select(tag => FetchWaifuImageAsync(tag)).ToList();
            var results = await Task.WhenAll(tasks);

            images.AddRange(results.Where(img => img != null)!);

            // If we don't have enough, fetch more with random tags (with attempt limit)
            int fetchAttempts = 0;
            while (images.Count < count && fetchAttempts < MaxFetchAttempts)
            {
                fetchAttempts++;
                var randomTag = _waifuTags[_random.Next(_waifuTags.Length)];
                var image = await FetchWaifuImageAsync(randomTag);
                if (image != null)
                {
                    images.Add(image);
                }

                // If we hit rate limits during this process, break out
                if (DateTime.UtcNow - _lastRateLimitHit < TimeSpan.FromMinutes(1))
                {
                    _logger.LogWarning("Breaking out of fetch loop due to recent rate limiting");
                    break;
                }
            }

            if (fetchAttempts >= MaxFetchAttempts)
            {
                _logger.LogWarning("Reached maximum fetch attempts ({MaxAttempts}) for waifu images, returning {ImageCount} images",
                    MaxFetchAttempts, images.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching waifu images");
        }

        return images.Take(count).ToList();
    }

    public async Task<List<NsfwImage>> GetAlternativeNsfwImagesAsync(int count = 5)
    {
        var results = new ConcurrentBag<NsfwImage>();

        try
        {
            // 1. Create list of available provider names
            var allProviders = new List<string>();

            // Add waifu.im if not rate limited
            var effectiveCooldown = _consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown;
            if (DateTime.UtcNow - _lastRateLimitHit >= effectiveCooldown)
            {
                allProviders.Add("waifu");
            }

            // Add other providers
            allProviders.AddRange(new[] { "purrbot", "hmtai", "nsfwapi", "hentaihaven" });

            // 2. Randomize and select 10 providers (or available count if less)
            var selectedProviders = allProviders
                .OrderBy(x => _random.Next())
                .Take(Math.Min(10, allProviders.Count))
                .ToList();

            // Pad with random selections if we need more to reach 10
            while (selectedProviders.Count < 10 && allProviders.Count > 0)
            {
                var randomProvider = allProviders[_random.Next(allProviders.Count)];
                selectedProviders.Add(randomProvider);
            }

            // Take only what we need for the count
            selectedProviders = selectedProviders.Take(count).ToList();

            // 3. Use providers.Select(Task.Run...) to execute in parallel
            var tasks = selectedProviders.Select(provider => Task.Run(async () =>
            {
                try
                {
                    var image = await CallProviderAsync(provider);
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

            _logger.LogInformation("Fetched {ActualCount} out of {RequestedCount} NSFW images from {ProviderCount} randomized providers",
                results.Count, count, selectedProviders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching alternative NSFW images");
        }

        // Remove duplicates and return up to count images
        return results
            .GroupBy(img => img.Url)
            .Select(group => group.First())
            .Take(count)
            .ToList();
    }

    private async Task<NsfwImage?> CallProviderAsync(string providerName)
    {
        // 4. Log source URL before API call
        _logger.LogDebug("Calling NSFW provider: {Provider}", providerName);

        return providerName switch
        {
            "waifu" => await FetchWaifuImageAsync(_nsfwTags[_random.Next(_nsfwTags.Length)]),
            "purrbot" => await FetchSingleFromPurrbotAsync(),
            "hmtai" => await FetchSingleFromHmtaiAsync(),
            "nsfwapi" => await FetchSingleFromNSFWAPIAsync(),
            "hentaihaven" => await FetchSingleFromHentaiHavenAPIAsync(),
            _ => null
        };
    }


    private async Task<NsfwImage?> FetchWaifuImageAsync(string tag)
    {
        // Use the search endpoint for random images with proper v5 API format
        // First try with the specific tag
        var result = await TryFetchWaifuImageWithUrl($"{WaifuApiBaseUrl}/search?included_tags={tag}&is_nsfw=true", tag);

        // If that fails, try without specific tag for random NSFW
        if (result == null)
        {
            result = await TryFetchWaifuImageWithUrl($"{WaifuApiBaseUrl}/search?is_nsfw=true", "random-nsfw");
        }

        return result;
    }

    private async Task<NsfwImage?> TryFetchWaifuImageWithUrl(string url, string tagForLogging)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Add required Accept-Version header
            request.Headers.Add("Accept-Version", WaifuApiVersion);

            _logger.LogInformation("Making request to waifu.im: {Url}", url);

            // Add a small delay between requests to avoid hitting rate limits
            await Task.Delay(200); // 200ms delay between requests

            using var response = await _httpClient.SendAsync(request);

            // Handle rate limiting and forbidden responses
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _lastRateLimitHit = DateTime.UtcNow;
                _consecutiveRateLimits++;

                // Check for Retry-After header as mentioned in API docs
                var retryAfterSeconds = 0;
                if (response.Headers.RetryAfter != null)
                {
                    retryAfterSeconds = (int)(response.Headers.RetryAfter.Delta?.TotalSeconds ?? 0);
                }

                // Use server-provided retry time or default cooldown
                var effectiveCooldown = retryAfterSeconds > 0
                    ? TimeSpan.FromSeconds(retryAfterSeconds)
                    : (_consecutiveRateLimits > 3 ? TimeSpan.FromMinutes(20) : _rateLimitCooldown);

                _logger.LogWarning("Rate limited by waifu.im API for tag: {Tag}. Cooldown activated for {Cooldown} seconds (consecutive: {Count}, server retry-after: {RetryAfter}s)",
                    tagForLogging, effectiveCooldown.TotalSeconds, _consecutiveRateLimits, retryAfterSeconds);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var responseBody = await response.Content.ReadAsStringAsync();

                // Check if this is the HTML access denied page (indicating service blocking)
                if (responseBody.Contains("Access Denied") && responseBody.Contains("<!DOCTYPE html>"))
                {
                    _logger.LogWarning("waifu.im service is blocking access - switching to permanent fallback mode for tag: {Tag}", tagForLogging);

                    // Treat this as a permanent service issue and activate extended cooldown
                    _lastRateLimitHit = DateTime.UtcNow;
                    _consecutiveRateLimits = 10; // Force extended cooldown to rely on fallback services
                }
                else
                {
                    _logger.LogWarning("Forbidden response from waifu.im for tag: {Tag}. Response: {Response}", tagForLogging, responseBody);
                }

                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} response from waifu.im for tag: {Tag}",
                    response.StatusCode, tagForLogging);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);

            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesElement) &&
                imagesElement.GetArrayLength() > 0)
            {
                // Reset consecutive rate limit counter on successful response
                _consecutiveRateLimits = 0;

                var firstImage = imagesElement[0];
                return new NsfwImage
                {
                    Url = firstImage.GetProperty("url").GetString() ?? string.Empty,
                    Source = "waifu.im",
                    Tags = tagForLogging,
                    Width = firstImage.TryGetProperty("width", out var width) ? width.GetInt32() : null,
                    Height = firstImage.TryGetProperty("height", out var height) ? height.GetInt32() : null,
                    Artist = firstImage.TryGetProperty("artist", out var artist) &&
                             artist.TryGetProperty("name", out var artistName)
                             ? artistName.GetString() : null
                };
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle specific HTTP exceptions
            if (ex.Message.Contains("429"))
            {
                _lastRateLimitHit = DateTime.UtcNow;
                _logger.LogWarning("Rate limited by waifu.im API for tag: {Tag}. Cooldown activated", tagForLogging);
            }
            else if (ex.Message.Contains("403"))
            {
                _logger.LogWarning("Forbidden response from waifu.im for tag: {Tag}. Tag may be restricted", tagForLogging);
            }
            else
            {
                _logger.LogWarning(ex, "HTTP error fetching image from waifu.im for tag: {Tag}", tagForLogging);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from waifu.im for tag: {Tag}", tagForLogging);
        }

        return null;
    }

    private async Task<List<NsfwImage>> FetchFromPurrbotAsync(int count)
    {
        var images = new List<NsfwImage>();
        var categories = new[] { "hentai", "ecchi", "lewdneko", "ero" };

        try
        {
            // Purrbot API endpoints - using their NSFW image categories
            var shuffledCategories = categories.OrderBy(x => _random.Next()).ToArray();

            foreach (var category in shuffledCategories)
            {
                for (int i = 0; i < Math.Ceiling((double)count / categories.Length) && images.Count < count; i++)
                {
                    try
                    {
                        var url = $"https://purrbot.site/api/img/nsfw/{category}/img";
                        var response = await _httpClient.GetStringAsync(url);
                        var jsonDoc = JsonDocument.Parse(response);

                        if (jsonDoc.RootElement.TryGetProperty("link", out var linkElement))
                        {
                            var imageUrl = linkElement.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !images.Any(img => img.Url == imageUrl))
                            {
                                images.Add(new NsfwImage
                                {
                                    Url = imageUrl,
                                    Source = "purrbot.site",
                                    Tags = category
                                });
                            }
                        }

                        // Small delay to be respectful to the API
                        await Task.Delay(150);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch from Purrbot category: {Category}", category);
                    }
                }

                if (images.Count >= count) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from Purrbot API");
        }

        return images;
    }

    private async Task<List<NsfwImage>> FetchFromHmtaiAsync(int count)
    {
        var images = new List<NsfwImage>();
        var endpoints = new[] { "hentai", "hmidriff", "hthigh", "hass", "hboobs", "hentai/gif" };

        try
        {
            // hmtai API endpoints - shuffle for variety
            var shuffledEndpoints = endpoints.OrderBy(x => _random.Next()).ToArray();

            foreach (var endpoint in shuffledEndpoints)
            {
                for (int i = 0; i < Math.Ceiling((double)count / endpoints.Length) && images.Count < count; i++)
                {
                    try
                    {
                        var url = $"https://hmtai.hatsunia.cfd/v2/{endpoint}";
                        var response = await _httpClient.GetStringAsync(url);
                        var jsonDoc = JsonDocument.Parse(response);

                        if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                        {
                            var imageUrl = urlElement.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !images.Any(img => img.Url == imageUrl))
                            {
                                images.Add(new NsfwImage
                                {
                                    Url = imageUrl,
                                    Source = "hmtai.hatsunia.cfd",
                                    Tags = endpoint.Replace("/", "_")
                                });
                            }
                        }

                        // Small delay to be respectful to the API
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch from hmtai endpoint: {Endpoint}", endpoint);
                    }
                }

                if (images.Count >= count) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from hmtai API");
        }

        return images;
    }

    private async Task<NsfwImage?> FetchSingleFromPurrbotAsync()
    {
        var categories = new[] { "hentai", "ecchi", "lewdneko", "ero" };
        var category = categories[_random.Next(categories.Length)];

        try
        {
            var url = $"https://purrbot.site/api/img/nsfw/{category}/img";
            _logger.LogInformation("Making request to purrbot: {Url}", url);
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
                        Source = "purrbot.site",
                        Tags = category
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch single image from Purrbot category: {Category}", category);
        }

        return null;
    }

    private async Task<NsfwImage?> FetchSingleFromHmtaiAsync()
    {
        var endpoints = new[] { "hentai", "hmidriff", "hthigh", "hass", "hboobs", "hentai/gif" };
        var endpoint = endpoints[_random.Next(endpoints.Length)];

        try
        {
            var url = $"https://hmtai.hatsunia.cfd/v2/{endpoint}";
            _logger.LogInformation("Making request to hmtai: {Url}", url);
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
            {
                var imageUrl = urlElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new NsfwImage
                    {
                        Url = imageUrl,
                        Source = "hmtai.hatsunia.cfd",
                        Tags = endpoint.Replace("/", "_")
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch single image from hmtai endpoint: {Endpoint}", endpoint);
        }

        return null;
    }

    private async Task<NsfwImage?> FetchSingleFromNSFWAPIAsync()
    {
        var categories = new[] { "hentai", "ecchi", "lewdneko", "trap", "futanari" };
        var category = categories[_random.Next(categories.Length)];

        try
        {
            var url = $"https://nsfw-api.onrender.com/{category}";
            _logger.LogInformation("Making request to nsfw-api: {Url}", url);
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
            {
                var imageUrl = urlElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new NsfwImage
                    {
                        Url = imageUrl,
                        Source = "nsfw-api.onrender.com",
                        Tags = category
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch single image from NSFW-API category: {Category}", category);
        }

        return null;
    }

    private async Task<NsfwImage?> FetchSingleFromHentaiHavenAPIAsync()
    {
        var endpoints = new[] { "random", "latest", "trending" };
        var endpoint = endpoints[_random.Next(endpoints.Length)];

        try
        {
            var url = $"https://hentai-api.herokuapp.com/api/v1/{endpoint}";
            _logger.LogInformation("Making request to hentai-api: {Url}", url);
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("image", out var imageElement))
            {
                var imageUrl = imageElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new NsfwImage
                    {
                        Url = imageUrl,
                        Source = "hentai-api.herokuapp.com",
                        Tags = endpoint
                    };
                }
            }
            else if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
            {
                var imageUrl = urlElement.GetString();
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    return new NsfwImage
                    {
                        Url = imageUrl,
                        Source = "hentai-api.herokuapp.com",
                        Tags = endpoint
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to fetch single image from HentaiHaven API endpoint: {Endpoint}", endpoint);
        }

        return null;
    }

    private async Task<List<NsfwImage>> FetchFromNSFWAPIAsync(int count)
    {
        var images = new List<NsfwImage>();
        var categories = new[] { "hentai", "ecchi", "lewdneko", "trap", "futanari" };

        try
        {
            var shuffledCategories = categories.OrderBy(x => _random.Next()).ToArray();

            foreach (var category in shuffledCategories)
            {
                for (int i = 0; i < Math.Ceiling((double)count / categories.Length) && images.Count < count; i++)
                {
                    try
                    {
                        var url = $"https://nsfw-api.onrender.com/{category}";
                        var response = await _httpClient.GetStringAsync(url);
                        var jsonDoc = JsonDocument.Parse(response);

                        if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                        {
                            var imageUrl = urlElement.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !images.Any(img => img.Url == imageUrl))
                            {
                                images.Add(new NsfwImage
                                {
                                    Url = imageUrl,
                                    Source = "nsfw-api.onrender.com",
                                    Tags = category
                                });
                            }
                        }

                        await Task.Delay(180);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch from NSFW-API category: {Category}", category);
                    }
                }

                if (images.Count >= count) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from NSFW-API");
        }

        return images;
    }

    private async Task<List<NsfwImage>> FetchFromHentaiHavenAPIAsync(int count)
    {
        var images = new List<NsfwImage>();
        var endpoints = new[] { "random", "latest", "trending" };

        try
        {
            foreach (var endpoint in endpoints)
            {
                for (int i = 0; i < Math.Ceiling((double)count / endpoints.Length) && images.Count < count; i++)
                {
                    try
                    {
                        var url = $"https://hentai-api.herokuapp.com/api/v1/{endpoint}";
                        var response = await _httpClient.GetStringAsync(url);
                        var jsonDoc = JsonDocument.Parse(response);

                        if (jsonDoc.RootElement.TryGetProperty("image", out var imageElement))
                        {
                            var imageUrl = imageElement.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !images.Any(img => img.Url == imageUrl))
                            {
                                images.Add(new NsfwImage
                                {
                                    Url = imageUrl,
                                    Source = "hentai-api.herokuapp.com",
                                    Tags = endpoint
                                });
                            }
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                        {
                            var imageUrl = urlElement.GetString();
                            if (!string.IsNullOrEmpty(imageUrl) && !images.Any(img => img.Url == imageUrl))
                            {
                                images.Add(new NsfwImage
                                {
                                    Url = imageUrl,
                                    Source = "hentai-api.herokuapp.com",
                                    Tags = endpoint
                                });
                            }
                        }

                        await Task.Delay(250);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch from HentaiHaven API endpoint: {Endpoint}", endpoint);
                    }
                }

                if (images.Count >= count) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from HentaiHaven API");
        }

        return images;
    }
}