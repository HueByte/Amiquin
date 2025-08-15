using Amiquin.Core.Models;
using Amiquin.Core.Services.Nsfw.Providers;
using Amiquin.Core.Services.Scrappers;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

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
    private readonly IEnumerable<INsfwProvider> _providers;
    private readonly IScrapper _scrapper;
    private readonly Random _random = new();

    // Circuit breaker for rate limiting
    private static DateTime _lastRateLimitHit = DateTime.MinValue;
    private static readonly TimeSpan _rateLimitCooldown = TimeSpan.FromMinutes(10);
    private static int _consecutiveRateLimits = 0;

    public NsfwApiService(ILogger<NsfwApiService> logger, IEnumerable<INsfwProvider> providers, IScrapper scrapper)
    {
        _logger = logger;
        _providers = providers;
        _scrapper = scrapper;

        var providerNames = _providers.Select(p => p.Name).ToList();
        if (_scrapper.IsEnabled)
        {
            providerNames.Add($"Scrapper({_scrapper.SourceName})");
        }

        _logger.LogInformation("NSFW API service initialized with providers: {Providers}",
            string.Join(", ", providerNames));
    }

    public async Task<List<NsfwImage>> GetDailyNsfwImagesAsync(int waifuCount = 5, int otherCount = 5)
    {
        return await GetNsfwImagesAsync(waifuCount + otherCount);
    }

    public async Task<NsfwApiResult> GetNsfwImagesWithStatusAsync(int waifuCount = 2, int otherCount = 8)
    {
        var result = new NsfwApiResult();

        // Check if currently rate-limited
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
    /// <param name="preferredProviders">Optional list of preferred provider names to use</param>
    /// <returns>List of NSFW images</returns>
    private async Task<List<NsfwImage>> GetNsfwImagesAsync(int count = 8, string[]? preferredProviders = null)
    {
        var results = new ConcurrentBag<NsfwImage>();

        try
        {
            // Get available providers
            var availableProviders = GetAvailableProviders(preferredProviders);

            if (!availableProviders.Any())
            {
                _logger.LogWarning("No available providers for NSFW images");
                return new List<NsfwImage>();
            }

            // Calculate how many images to get from scrapper vs providers
            var scrapperCount = _scrapper.IsEnabled ? count : 0; // Up to half from scrapper
            var providerCount = count - scrapperCount;

            var tasks = new List<Task>();

            // Get images from providers
            if (providerCount > 0 && availableProviders.Any())
            {
                var selectedProviders = availableProviders
                    .OrderBy(x => _random.Next())
                    .Take(Math.Min(providerCount, availableProviders.Count()))
                    .ToList();

                _logger.LogInformation("Selected {Count} randomized NSFW providers: {Providers}",
                    selectedProviders.Count(), string.Join(", ", selectedProviders.Select(p => p.Name)));

                // Execute provider calls in parallel
                tasks.AddRange(selectedProviders.Select(provider => Task.Run(async () =>
                {
                    try
                    {
                        var image = await provider.FetchImageAsync();
                        if (image != null && !string.IsNullOrEmpty(image.Url))
                        {
                            results.Add(image);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Provider {Provider} failed to fetch image", provider.Name);
                    }
                })));
            }

            // Get images from scrapper
            if (scrapperCount > 0 && _scrapper.IsEnabled)
            {
                var scrapperTask = Task.Run(async () =>
                {
                    try
                    {
                        var images = await _scrapper.ScrapeAsync<NsfwImage>(scrapperCount);
                        foreach (var image in images)
                        {
                            if (image != null && !string.IsNullOrEmpty(image.Url))
                            {
                                results.Add(image);
                            }
                        }
                        _logger.LogDebug("Scrapper {SourceName} successfully fetched {Count} images",
                            _scrapper.SourceName, images.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Scrapper {SourceName} failed to fetch images", _scrapper.SourceName);
                    }
                });
                tasks.Add(scrapperTask);
            }

            await Task.WhenAll(tasks);

            var totalSourcesUsed = (providerCount > 0 ? 1 : 0) + (scrapperCount > 0 ? 1 : 0);

            _logger.LogInformation("Fetched {ActualCount} out of {RequestedCount} NSFW images from {SourceCount} source types",
                results.Count, count, totalSourcesUsed);
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
    /// Gets list of available providers, filtering out unavailable ones
    /// </summary>
    private IEnumerable<INsfwProvider> GetAvailableProviders(string[]? preferredProviders = null)
    {
        var allProviders = _providers.ToList();

        // Filter by preferred providers if specified
        if (preferredProviders != null && preferredProviders.Length > 0)
        {
            allProviders = allProviders.Where(p => preferredProviders.Contains(p.Name)).ToList();
        }

        // Filter out unavailable providers
        var availableProviders = allProviders.Where(p => p.IsAvailable).ToList();

        if (availableProviders.Count != allProviders.Count)
        {
            var unavailableProviders = allProviders.Except(availableProviders).Select(p => p.Name);
            _logger.LogDebug("Excluding providers due to availability: {Providers}",
                string.Join(", ", unavailableProviders));
        }

        return availableProviders;
    }


}