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
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(5);
    
    private const int MaxRetryAttempts = 3;
    private const int MaxFetchAttempts = 10; // Prevent infinite loops

    private readonly string[] _waifuTags = new[]
    {
        "waifu", "maid", "marin-kitagawa", "mori-calliope", "raiden-shogun",
        "oppai", "selfies", "uniform", "kitsune"
    };

    private readonly string[] _alternativeTags = new[]
    {
        "ass", "hentai", "milf", "oral", "paizuri", "ecchi", "ero"
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
            if (DateTime.UtcNow - _lastRateLimitHit < _rateLimitCooldown)
            {
                _logger.LogInformation("Skipping waifu.im due to recent rate limiting, cooldown until {CooldownEnd}", 
                    _lastRateLimitHit.Add(_rateLimitCooldown));
                return images;
            }

            // Get random tags for variety
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

            // If we still need more images and not rate limited, try waifu.im with alternative tags
            if (images.Count < count && DateTime.UtcNow - _lastRateLimitHit >= _rateLimitCooldown)
            {
                var remainingCount = Math.Min(count - images.Count, MaxFetchAttempts);
                var selectedTags = _alternativeTags.OrderBy(x => _random.Next()).Take(remainingCount).ToList();
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

        try
        {
            var url = $"{_waifuApiOptions.BaseUrl}/search?included_tags={tag}&is_nsfw=true";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // Add required Accept-Version header
            request.Headers.Add("Accept-Version", _waifuApiOptions.Version);
            
            // Add authentication header if token is configured
            if (_waifuApiOptions.HasAuthentication)
            {
                request.Headers.Add("Authorization", $"Bearer {_waifuApiOptions.Token}");
            }
            
            using var response = await _httpClient.SendAsync(request);
            
            // Handle rate limiting and forbidden responses
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _lastRateLimitHit = DateTime.UtcNow;
                _logger.LogWarning("Rate limited by waifu.im API for tag: {Tag}. Cooldown activated for {Cooldown} minutes", 
                    tag, _rateLimitCooldown.TotalMinutes);
                return null;
            }
            
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Forbidden response from waifu.im for tag: {Tag}. Tag may be restricted", tag);
                return null;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("HTTP {StatusCode} response from waifu.im for tag: {Tag}", 
                    response.StatusCode, tag);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(responseContent);

            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesElement) &&
                imagesElement.GetArrayLength() > 0)
            {
                var firstImage = imagesElement[0];
                return new NsfwImage
                {
                    Url = firstImage.GetProperty("url").GetString() ?? string.Empty,
                    Source = "waifu.im",
                    Tags = tag,
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
                _logger.LogWarning("Rate limited by waifu.im API for tag: {Tag}. Cooldown activated", tag);
            }
            else if (ex.Message.Contains("403"))
            {
                _logger.LogWarning("Forbidden response from waifu.im for tag: {Tag}. Tag may be restricted", tag);
            }
            else
            {
                _logger.LogWarning(ex, "HTTP error fetching image from waifu.im for tag: {Tag}", tag);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from waifu.im for tag: {Tag}", tag);
        }

        return null;
    }

    private async Task<List<NsfwImage>> FetchFromWaifuPicsNsfwAsync(int count)
    {
        var images = new List<NsfwImage>();
        var categories = new[] { "waifu", "neko", "trap", "blowjob" };

        try
        {
            foreach (var category in categories.Take(count))
            {
                try
                {
                    var url = $"https://api.waifu.pics/nsfw/{category}";
                    var response = await _httpClient.GetStringAsync(url);
                    var jsonDoc = JsonDocument.Parse(response);

                    if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                    {
                        images.Add(new NsfwImage
                        {
                            Url = urlElement.GetString() ?? string.Empty,
                            Source = "waifu.pics",
                            Tags = category
                        });
                    }

                    if (images.Count >= count) break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch from waifu.pics category: {Category}", category);
                }
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
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from nekos.best");
        }

        return images;
    }
}