using System.Net;
using System.Text.Json;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly WaifuApiOptions _waifuApiOptions;
    private readonly Random _random = new();
    
    // Circuit breaker for rate limiting
    private static DateTime _lastRateLimitHit = DateTime.MinValue;
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(10); // Increased cooldown
    private static int _consecutiveRateLimits = 0;
    
    private const int MaxRetryAttempts = 3;
    private const int MaxFetchAttempts = 10; // Prevent infinite loops

    private readonly string[] _waifuTags = new[]
    {
        "waifu", "maid", "uniform", "kitsune", "elf"
    };

    private readonly string[] _nsfwTags = new[]
    {
        "oppai", "ass", "hentai", "milf", "oral", "paizuri", "ecchi"
    };

    public NsfwApiService(ILogger<NsfwApiService> logger, HttpClient httpClient, IOptions<WaifuApiOptions> waifuApiOptions)
    {
        _logger = logger;
        _httpClient = httpClient;
        _waifuApiOptions = waifuApiOptions.Value;
        
        // Log authentication status on startup
        if (_waifuApiOptions.Enabled)
        {
            if (_waifuApiOptions.HasAuthentication)
            {
                _logger.LogInformation("Waifu API initialized with authentication (v{Version})", _waifuApiOptions.Version);
            }
            else
            {
                _logger.LogInformation("Waifu API initialized without authentication (v{Version}) - consider adding a token for better rate limits", _waifuApiOptions.Version);
            }
        }
        else
        {
            _logger.LogInformation("Waifu API is disabled");
        }
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

    public async Task<NsfwApiResult> GetNsfwImagesWithStatusAsync(int waifuCount = 5, int otherCount = 5)
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
        var images = new List<NsfwImage>();

        try
        {
            // First priority: nekos.best API
            var nekosBestImages = await FetchFromNekosBestAsync(count);
            images.AddRange(nekosBestImages);

            // If we still need more images and not rate limited, try waifu.im with NSFW tags
            var effectiveCooldown = _consecutiveRateLimits > 3 
                ? TimeSpan.FromMinutes(20) 
                : _rateLimitCooldown;
                
            if (images.Count < count && DateTime.UtcNow - _lastRateLimitHit >= effectiveCooldown)
            {
                var remainingCount = Math.Min(count - images.Count, MaxFetchAttempts);
                var selectedTags = _nsfwTags.OrderBy(x => _random.Next()).Take(remainingCount).ToList();
                var tasks = selectedTags.Select(tag => FetchWaifuImageAsync(tag)).ToList();
                var results = await Task.WhenAll(tasks);
                images.AddRange(results.Where(img => img != null)!);
            }

            // Last resort: waifu.pics NSFW endpoint
            if (images.Count < count)
            {
                var waifuPicsImages = await FetchFromWaifuPicsNsfwAsync(count - images.Count);
                images.AddRange(waifuPicsImages);
            }

            // If still not enough, log warning but don't keep trying
            if (images.Count < count)
            {
                _logger.LogInformation("Could only fetch {ActualCount} out of {RequestedCount} alternative NSFW images due to API limitations", 
                    images.Count, count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching alternative NSFW images");
        }

        return images.Take(count).ToList();
    }

    private async Task<NsfwImage?> FetchWaifuImageAsync(string tag)
    {
        // Skip if Waifu API is disabled
        if (!_waifuApiOptions.Enabled)
        {
            return null;
        }

        // Use the search endpoint for random images with proper v5 API format
        // First try with the specific tag
        var result = await TryFetchWaifuImageWithUrl($"{_waifuApiOptions.BaseUrl}/search?included_tags={tag}&is_nsfw=true", tag);
        
        // If that fails, try without specific tag for random NSFW
        if (result == null)
        {
            result = await TryFetchWaifuImageWithUrl($"{_waifuApiOptions.BaseUrl}/search?is_nsfw=true", "random-nsfw");
        }
        
        return result;
    }
    
    private async Task<NsfwImage?> TryFetchWaifuImageWithUrl(string url, string tagForLogging)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // Add required Accept-Version header
            request.Headers.Add("Accept-Version", _waifuApiOptions.Version);
            
            // Add authentication header if token is configured
            if (_waifuApiOptions.HasAuthentication)
            {
                request.Headers.Add("Authorization", $"Bearer {_waifuApiOptions.Token}");
            }
            
            _logger.LogDebug("Making waifu.im request: {Url}", url);
            
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

    private async Task<List<NsfwImage>> FetchFromWaifuPicsNsfwAsync(int count)
    {
        var images = new List<NsfwImage>();
        var categories = new[] { "waifu", "neko", "trap", "blowjob", "ass", "hentai", "milf", "oral", "paizuri", "ecchi" };

        try
        {
            // Randomize categories and fetch multiple images per category for better variety
            var shuffledCategories = categories.OrderBy(x => _random.Next()).ToArray();
            var imagesPerCategory = Math.Max(1, count / 4); // At least 1 per category, distribute count
            
            foreach (var category in shuffledCategories)
            {
                // Fetch multiple images from each category to increase variety
                for (int i = 0; i < imagesPerCategory && images.Count < count * 2; i++) // Fetch extra for filtering
                {
                    try
                    {
                        var url = $"https://api.waifu.pics/nsfw/{category}";
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
                                    Source = "waifu.pics",
                                    Tags = category
                                });
                            }
                        }

                        // Small delay to avoid overwhelming the API
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to fetch from waifu.pics category: {Category}", category);
                    }
                }

                if (images.Count >= count) break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from waifu.pics");
        }

        return images;
    }

    private async Task<List<NsfwImage>> FetchFromNekosBestAsync(int count)
    {
        var images = new List<NsfwImage>();

        try
        {
            // nekos.best API for hentai content
            var url = $"https://nekos.best/api/v2/hentai?amount={Math.Min(count, 20)}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Amiquin-Bot/1.0");
            
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("nekos.best returned {StatusCode}: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                return images;
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);

            if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElement))
            {
                foreach (var result in resultsElement.EnumerateArray())
                {
                    if (result.TryGetProperty("url", out var urlProp))
                    {
                        images.Add(new NsfwImage
                        {
                            Url = urlProp.GetString() ?? string.Empty,
                            Source = "nekos.best",
                            Tags = "hentai",
                            Artist = result.TryGetProperty("artist_name", out var artist) 
                                     ? artist.GetString() : null,
                            Title = result.TryGetProperty("source_url", out var source) 
                                    ? source.GetString() : null
                        });
                    }

                    if (images.Count >= count) break;
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error connecting to nekos.best. Skipping this source.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from nekos.best");
        }

        return images;
    }
}