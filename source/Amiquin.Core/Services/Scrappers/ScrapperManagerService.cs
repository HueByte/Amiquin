using Amiquin.Core.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Service for managing multiple scrapper providers
/// </summary>
public class ScrapperManagerService : IScrapperManagerService
{
    private readonly ILogger<ScrapperManagerService> _logger;
    private readonly List<IImageScraper> _imageScrapers;
    private readonly List<IDataScrapper> _dataScrapers;
    private readonly Random _random;
    private readonly ScrapperOptions _options;
    private readonly IMemoryCache _memoryCache;

    public ScrapperManagerService(
        ILogger<ScrapperManagerService> logger,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        IMemoryCache memoryCache,
        IOptions<ScrapperOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _memoryCache = memoryCache;
        _random = new Random();
        _imageScrapers = new List<IImageScraper>();
        _dataScrapers = new List<IDataScrapper>();

        InitializeScrapers(httpClientFactory, loggerFactory);
    }

    public IEnumerable<IImageScraper> GetImageScrapers() => _imageScrapers.Where(s => s.IsEnabled);

    public IEnumerable<IDataScrapper> GetDataScrapers() => _dataScrapers.Where(s => s.IsEnabled);

    public IImageScraper? GetImageScraper(string sourceName) =>
        _imageScrapers.FirstOrDefault(s => s.SourceName.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

    public async Task<string[]> ScrapeGalleryImagesAsync(int count = 10, bool randomize = true, bool useCache = true)
    {
        var enabledScrapers = GetImageScrapers().ToList();

        if (!enabledScrapers.Any())
        {
            _logger.LogWarning("No enabled image scrapers available for gallery");
            return Array.Empty<string>();
        }

        _logger.LogInformation("Starting gallery scraping with {Count} scrapers for {RequestedCount} images",
            enabledScrapers.Count, count);

        var allImageUrls = new List<string>();

        // Scrape from each enabled provider
        foreach (var scraper in enabledScrapers)
        {
            try
            {
                // For gallery, we want to get a good variety, so let's get more images per scraper
                // We'll get 5 random "albums" worth of content by requesting more and randomizing
                var perScraperCount = Math.Max(5, count / enabledScrapers.Count + 5);

                _logger.LogDebug("Scraping {Count} images from {SourceName}", perScraperCount, scraper.SourceName);

                var scraperUrls = await scraper.ScrapeImagesUrlsAsync(perScraperCount, true, useCache);
                if (scraperUrls.Length > 0)
                {
                    allImageUrls.AddRange(scraperUrls);
                    _logger.LogDebug("Added {Count} URLs from {SourceName}", scraperUrls.Length, scraper.SourceName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to scrape from {SourceName}", scraper.SourceName);
            }
        }

        if (allImageUrls.Count == 0)
        {
            _logger.LogWarning("No images were scraped from any provider");
            return Array.Empty<string>();
        }

        // Remove duplicates and validate
        var preDeduplicationCount = allImageUrls.Count;
        allImageUrls = allImageUrls.Distinct().ToList();

        if (allImageUrls.Count != preDeduplicationCount)
        {
            _logger.LogDebug("Removed {DuplicateCount} duplicate URLs across all providers, {UniqueCount} unique URLs remaining",
                preDeduplicationCount - allImageUrls.Count, allImageUrls.Count);
        }

        // Additional validation pass for gallery
        var preValidationCount = allImageUrls.Count;
        allImageUrls = allImageUrls.Where(url => !string.IsNullOrWhiteSpace(url) && url.Length < 2000).ToList();

        if (allImageUrls.Count != preValidationCount)
        {
            _logger.LogWarning("Gallery validation filtered out {FilteredCount} URLs, {RemainingCount} remaining",
                preValidationCount - allImageUrls.Count, allImageUrls.Count);
        }

        if (allImageUrls.Count == 0)
        {
            _logger.LogWarning("No valid images remaining after gallery validation");
            return Array.Empty<string>();
        }

        // Randomize and return the requested count
        if (randomize)
        {
            allImageUrls = allImageUrls.OrderBy(x => _random.Next()).ToList();
        }

        var result = allImageUrls.Take(count).ToArray();

        _logger.LogInformation("Gallery scraping completed: {ReturnedCount} images from {TotalScraped} total",
            result.Length, allImageUrls.Count);

        // Final validation log for debugging
        for (int i = 0; i < result.Length; i++)
        {
            _logger.LogDebug("Gallery URL {Position}: {Url}", i, result[i]);
        }

        return result;
    }

    public void ClearAllCaches()
    {
        foreach (var scraper in _imageScrapers)
        {
            try
            {
                scraper.ClearCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear cache for scraper {SourceName}", scraper.SourceName);
            }
        }

        _logger.LogInformation("Cleared caches for all scrapers");
    }

    private void InitializeScrapers(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _logger.LogInformation("Initializing ScrapperManagerService with cache size: {CacheSize}, expiration: {CacheExpirationMinutes} minutes",
            _options.CacheSize, _options.CacheExpirationMinutes);

        if (_options.Providers == null || _options.Providers.Length == 0)
        {
            _logger.LogWarning("No scrapper providers configured - Providers is {ProvidersStatus}",
                _options.Providers == null ? "null" : "empty array");
            return;
        }

        _logger.LogInformation("Found {ProviderCount} scrapper providers in configuration", _options.Providers.Length);

        foreach (var providerConfig in _options.Providers)
        {
            try
            {
                if (!providerConfig.Enabled)
                {
                    _logger.LogDebug("Skipping disabled scrapper provider: {SourceName}", providerConfig.SourceName);
                    continue;
                }

                var httpClient = httpClientFactory.CreateClient($"Scrapper_{providerConfig.SourceName}");

                var scrapper = new ConfigurationBasedScrapper(
                    loggerFactory.CreateLogger<ConfigurationBasedScrapper>(),
                    httpClient,
                    providerConfig,
                    _memoryCache,
                    _options.CacheSize,
                    _options.CacheExpirationMinutes);

                _imageScrapers.Add(scrapper);
                _dataScrapers.Add(scrapper);

                _logger.LogInformation("Initialized scrapper for provider: {SourceName}", providerConfig.SourceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize scrapper for provider: {SourceName}",
                    providerConfig.SourceName);
            }
        }

        _logger.LogInformation("Initialized {ImageScrapperCount} image scrapers and {DataScrapperCount} data scrapers",
            _imageScrapers.Count, _dataScrapers.Count);
    }
}