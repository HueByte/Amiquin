using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Scrappers.Models;
using Amiquin.Core.Utilities;
using Amiquin.Core.Utilities.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Configuration-based scrapper that uses ScrapperStep for extraction
/// </summary>
public class ConfigurationBasedScrapper : IDataScrapper, IImageScraper
{
    private readonly ILogger<ConfigurationBasedScrapper> _logger;
    private readonly HttpClient _httpClient;
    private readonly ScrapperProviderOptions _options;
    private readonly IMemoryCache _memoryCache;
    private readonly Random _random;

    private readonly int _cacheSize;
    private readonly TimeSpan _cacheExpiration;

    private sealed record ScrapperUrlCache(List<string> Urls, DateTime CachedAt);

    public ConfigurationBasedScrapper(
        ILogger<ConfigurationBasedScrapper> logger,
        HttpClient httpClient,
        ScrapperProviderOptions options,
        IMemoryCache memoryCache,
        int cacheSize = 200,
        int cacheExpirationMinutes = 60)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options;
        _memoryCache = memoryCache;
        _random = new Random();
        _cacheSize = cacheSize;
        _cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);

        ConfigureHttpClient();
    }

    public string SourceName => _options.SourceName;
    public bool IsEnabled => _options.Enabled;

    public async Task<List<T>> ScrapeAsync<T>(int count = 10, bool randomize = false) where T : class
    {
        try
        {
            var extractedData = await ExecuteScrapingStepsAsync(count);
            return ProcessResults<T>(extractedData, count, randomize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape from {SourceName}", SourceName);
            return new List<T>();
        }
    }

    public List<T> ProcessResults<T>(List<string> extractedData, int count = 10, bool randomize = false) where T : class
    {
        if (typeof(T) == typeof(NsfwImage))
        {
            return ProcessNsfwImageResults(extractedData, count, randomize).Cast<T>().ToList();
        }

        _logger.LogWarning("Unsupported result type {Type} for {SourceName}", typeof(T).Name, SourceName);
        return new List<T>();
    }

    public async Task<string[]> GetImageUrlsAsync(int count = 10, bool randomize = false)
    {
        return await ScrapeImagesUrlsAsync(count, randomize, true);
    }

    public async Task<string[]> ScrapeImagesUrlsAsync(int count = 5, bool randomize = true, bool useCache = true)
    {
        try
        {
            var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ScrapperImageUrls, SourceName);
            var result = new List<string>();

            // Check cache if enabled
            ScrapperUrlCache cachedData;
            if (useCache && _memoryCache.TryGetTypedValue(cacheKey, out ScrapperUrlCache? cachedEntry) && cachedEntry is not null)
            {
                cachedData = cachedEntry;
                var isCacheValid = DateTime.UtcNow - cachedData.CachedAt < _cacheExpiration;
                if (!isCacheValid)
                {
                    _logger.LogDebug("Cache expired for {SourceName}, clearing", SourceName);
                    _memoryCache.Remove(cacheKey);
                    cachedData = new ScrapperUrlCache(new List<string>(), DateTime.MinValue);
                }
            }
            else
            {
                cachedData = new ScrapperUrlCache(new List<string>(), DateTime.MinValue);
            }

            // Hybrid strategy: Use mix of cache and fresh scraping when cache has enough data
            if (useCache && cachedData.Urls.Count >= 200)
            {
                var cacheCount = Math.Max(1, count / 2); // Use half from cache
                var scrapeCount = count - cacheCount; // Rest from fresh scraping

                _logger.LogInformation("Using hybrid strategy for {SourceName}: {CacheCount} from cache ({CachedTotal} available), {ScrapeCount} from fresh scraping",
                    SourceName, cacheCount, cachedData.Urls.Count, scrapeCount);

                // Get images from cache
                var fromCache = randomize
                    ? cachedData.Urls.OrderBy(x => _random.Next()).Take(cacheCount).ToList()
                    : cachedData.Urls.Take(cacheCount).ToList();
                result.AddRange(fromCache);

                // Get fresh images from scraping
                if (scrapeCount > 0)
                {
                    var freshImages = await ScrapeNewImagesAsync(scrapeCount, randomize);

                    // Add fresh images to result, avoiding duplicates with cache
                    var uniqueFreshImages = freshImages.Where(url => !fromCache.Contains(url)).ToList();
                    result.AddRange(uniqueFreshImages);

                    // Update cache with fresh images (merge and maintain cache size limit)
                    if (freshImages.Length > 0)
                    {
                        var updatedCache = cachedData.Urls.Concat(freshImages).Distinct().ToList();

                        // Limit cache size and keep most recent
                        if (updatedCache.Count > _cacheSize)
                        {
                            updatedCache = updatedCache.Skip(updatedCache.Count - _cacheSize).ToList();
                        }

                        _memoryCache.Set(cacheKey, new ScrapperUrlCache(updatedCache, DateTime.UtcNow), _cacheExpiration);
                        _logger.LogDebug("Updated cache for {SourceName}: added {NewCount} fresh images, cache now has {TotalCount} URLs",
                            SourceName, freshImages.Length, updatedCache.Count);
                    }
                }

                _logger.LogInformation("Hybrid result for {SourceName}: {FromCacheCount} from cache + {FreshCount} fresh = {TotalCount} total",
                    SourceName, fromCache.Count, result.Count - fromCache.Count, result.Count);
            }
            else if (useCache && cachedData.Urls.Count >= count)
            {
                // Use cache only if we have enough
                _logger.LogDebug("Using cached data for {SourceName}, cache has {Count} URLs", SourceName, cachedData.Urls.Count);

                result = randomize
                    ? cachedData.Urls.OrderBy(x => _random.Next()).Take(count).ToList()
                    : cachedData.Urls.Take(count).ToList();
            }
            else
            {
                // Cache miss or insufficient data - scrape new data and build cache
                _logger.LogDebug("Cache insufficient for {SourceName} (has {CacheCount}, need {RequestedCount}), scraping fresh data",
                    SourceName, cachedData.Urls.Count, count);

                var freshImages = await ScrapeNewImagesAsync(_cacheSize, true);

                if (freshImages.Length > 0)
                {
                    // Update cache
                    _memoryCache.Set(cacheKey, new ScrapperUrlCache(freshImages.ToList(), DateTime.UtcNow), _cacheExpiration);
                    _logger.LogDebug("Built cache for {SourceName} with {Count} fresh URLs", SourceName, freshImages.Length);

                    // Return requested count
                    result = randomize
                        ? freshImages.OrderBy(x => _random.Next()).Take(count).ToList()
                        : freshImages.Take(count).ToList();
                }
            }

            return result.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape image URLs from {SourceName}", SourceName);
            return Array.Empty<string>();
        }
    }

    private async Task<string[]> ScrapeNewImagesAsync(int count, bool randomize = true)
    {
        try
        {
            // Scrape fresh data
            var extractedData = await ExecuteScrapingStepsAsync(count * 2); // Get extra to account for potential failures
            var fullUrls = extractedData
                .Select(x => BuildFullUrl(x))
                .Distinct() // Remove duplicates
                .ToList();

            if (fullUrls.Count != extractedData.Count)
            {
                _logger.LogDebug("Removed {DuplicateCount} duplicate URLs, {UniqueCount} unique URLs remaining for {SourceName}",
                    extractedData.Count - fullUrls.Count, fullUrls.Count, SourceName);
            }

            // Validate URLs
            _logger.LogDebug("Validating {Count} fresh URLs for {SourceName}", fullUrls.Count, SourceName);
            var validUrls = await ValidateImageUrlsAsync(fullUrls.ToArray());

            if (validUrls.Length < fullUrls.Count)
            {
                _logger.LogDebug("URL validation filtered out {FilteredCount} invalid URLs from {TotalCount} fresh URLs for {SourceName}",
                    fullUrls.Count - validUrls.Length, fullUrls.Count, SourceName);
            }

            return validUrls;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape fresh images from {SourceName}", SourceName);
            return Array.Empty<string>();
        }
    }

    public Task<string[]> ValidateImageUrlsAsync(string[] urls)
    {
        var validUrls = new List<string>();
        var invalidCount = 0;

        for (int i = 0; i < urls.Length; i++)
        {
            var url = urls[i];
            try
            {
                if (IsValidImageUrl(url))
                {
                    validUrls.Add(url);
                }
                else
                {
                    invalidCount++;
                    _logger.LogDebug("Invalid image URL at position {Position} filtered out: {Url}", i, url);
                }
            }
            catch (Exception ex)
            {
                invalidCount++;
                _logger.LogError(ex, "Exception during URL validation at position {Position} for URL: {Url}", i, url);
            }
        }

        if (invalidCount > 0)
        {
            _logger.LogDebug("URL validation summary: {ValidCount} valid, {InvalidCount} invalid URLs", validUrls.Count, invalidCount);
        }

        return Task.FromResult(validUrls.ToArray());
    }

    public void ClearCache()
    {
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ScrapperImageUrls, SourceName);
        _memoryCache.Remove(cacheKey);
        _logger.LogInformation("Cache cleared for {SourceName}", SourceName);
    }

    private async Task<List<string>> ExecuteScrapingStepsAsync(int count = 10)
    {
        _logger.LogInformation("Starting scraping process for {SourceName} with {Count} results requested", SourceName, count);
        _logger.LogInformation("Using {StepCount} extraction steps", _options.ExtractionSteps.Count);
        _logger.LogDebug("Extraction steps: {Steps}", string.Join(", ", _options.ExtractionSteps.Select(s =>
            !string.IsNullOrEmpty(s.Name) ? $"{s.Name}: {s.Url}" : s.Url)));

        var currentData = new List<string>();
        foreach (var step in _options.ExtractionSteps)
        {
            // if last step and currentData has enough count of results stop the iteration
            if (step == _options.ExtractionSteps.Last() && currentData.Count >= count)
                break;

            try
            {
                var stepResults = new List<string>();

                _logger.LogInformation("Step repetition count for '{StepName}': {RepetitionCount}", step.Name, step.RepetitionCount);

                // Execute step multiple times if repetition is requested
                for (int i = 0; i < step.RepetitionCount; i++)
                {
                    try
                    {
                        var stepResult = await ExecuteStepAsync(step, currentData);
                        if (stepResult.Count > 0)
                        {
                            stepResults.AddRange(stepResult);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute step '{StepName}' for {SourceName}", step.Name, SourceName);
                    }
                }

                if (stepResults.Count == 0)
                {
                    var stepName = !string.IsNullOrEmpty(step.Name) ? step.Name : "Unnamed Step";
                    _logger.LogWarning("Step '{StepName}' returned no results after {RepetitionCount} attempts for {SourceName}",
                        stepName, step.RepetitionCount, SourceName);
                    return new List<string>();
                }

                // Randomize results if requested
                if (step.RandomizeResults)
                {
                    stepResults = stepResults.OrderBy(x => _random.Next()).ToList();
                }

                currentData = stepResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute step for {SourceName}", SourceName);
                return new List<string>();
            }
        }

        // Return up to the requested count
        _logger.LogInformation("Scraping completed for {SourceName}. Extracted {Count} results", SourceName, currentData.Count);
        _logger.LogDebug("Extracted data: {Data}", string.Join(" | ", currentData.Take(10))); // Log first 10 for brevity
        return currentData.Take(count * 2).ToList(); // Get extra to account for potential failures
    }

    private async Task<List<string>> ExecuteStepAsync(ScrapperStep step, List<string> previousResults)
    {
        var stepName = !string.IsNullOrEmpty(step.Name) ? step.Name : "Unnamed Step";
        var url = ProcessUrlVariables(step.Url, previousResults);

        _logger.LogInformation("Executing step '{StepName}' for {SourceName} with URL: {Url}", stepName, SourceName, url);
        var response = await _httpClient.GetStringAsync(url);

        var regex = new Regex(step.ExtractionRegex, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var matches = regex.Matches(response);

        var results = new List<string>();

        foreach (Match match in matches)
        {
            if (match.Groups.Count > step.CaptureGroup)
            {
                var capturedValue = match.Groups[step.CaptureGroup].Value;
                _logger.LogDebug("Match found in step '{StepName}': {MatchValue}", stepName, capturedValue);
                results.Add(capturedValue);
            }

            if (!step.ExtractMultiple)
                break;
        }

        // If we need to select the highest resolution, process the results
        if (step.SelectHighestResolution && results.Count > 1)
        {
            results = SelectHighestResolutionImages(results);
        }

        _logger.LogInformation("Step '{StepName}' extracted {Count} results from {SourceName}", stepName, results.Count, SourceName);
        _logger.LogDebug("Extracted results: {Results}", string.Join(" | ", results));
        await Task.Delay(100); // respect provider 
        return results;
    }

    private List<string> SelectHighestResolutionImages(List<string> imageUrls)
    {
        // Parse resolution from URLs and select the ones with highest resolution
        var urlsWithResolution = new List<(string url, int resolution)>();

        foreach (var url in imageUrls)
        {
            // Extract resolution from patterns like "1024x0" or "1680x0"
            var resolutionMatch = Regex.Match(url, @"\.(\d+)x0\.");
            if (resolutionMatch.Success && int.TryParse(resolutionMatch.Groups[1].Value, out var resolution))
            {
                urlsWithResolution.Add((url, resolution));
            }
            else
            {
                // If no resolution found, assume low resolution
                urlsWithResolution.Add((url, 0));
            }
        }

        // Group by resolution and select the highest resolution URLs
        var highestResolution = urlsWithResolution.Max(x => x.resolution);
        var highestResUrls = urlsWithResolution
            .Where(x => x.resolution == highestResolution)
            .Select(x => x.url)
            .ToList();

        _logger.LogDebug("Selected {Count} images with highest resolution {Resolution}px from {TotalCount} total images",
            highestResUrls.Count, highestResolution, imageUrls.Count);

        return highestResUrls;
    }

    private string ProcessUrlVariables(string url, List<string> previousResults)
    {
        var processedUrl = url;

        // Handle {random(start...end)} variables (existing functionality)
        var randomRangePattern = @"\{random\((\d+)\.\.\.(\d+)\)\}";
        var randomRangeMatches = Regex.Matches(processedUrl, randomRangePattern);

        foreach (Match match in randomRangeMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var start) &&
                int.TryParse(match.Groups[2].Value, out var end))
            {
                var randomValue = _random.Next(start, end + 1);
                processedUrl = processedUrl.Replace(match.Value, randomValue.ToString());
            }
        }

        // Handle {random} - generates a random number between 1-1000
        processedUrl = processedUrl.Replace("{random}", _random.Next(1, 1001).ToString());

        // Handle {randomResult} - picks a random value from the previous step results
        if (previousResults.Count > 0 && processedUrl.Contains("{randomResult}"))
        {
            var randomIndex = _random.Next(previousResults.Count);
            processedUrl = processedUrl.Replace("{randomResult}", previousResults[randomIndex]);
        }

        // Handle {result[index]} variables for previous step results
        var resultPattern = @"\{result\[(\d+)\]\}";
        var resultMatches = Regex.Matches(processedUrl, resultPattern);

        foreach (Match match in resultMatches)
        {
            if (int.TryParse(match.Groups[1].Value, out var index) &&
                index < previousResults.Count)
            {
                processedUrl = processedUrl.Replace(match.Value, previousResults[index]);
            }
        }

        // Handle {result} for the first/only result from previous step
        if (previousResults.Count > 0)
        {
            processedUrl = processedUrl.Replace("{result}", previousResults[0]);
        }

        return processedUrl;
    }

    private List<NsfwImage> ProcessNsfwImageResults(List<string> extractedData, int count = 5, bool randomize = false)
    {
        var results = new List<NsfwImage>();

        if (extractedData.Count == 0)
        {
            _logger.LogWarning("No extracted data to process for {SourceName}", SourceName);
            return results;
        }

        try
        {
            // For Luscious.net, the final extracted data should be image URLs
            // Take up to the requested count
            var imageUrls = extractedData.Take(count).ToList();

            foreach (var imageUrl in imageUrls)
            {
                if (string.IsNullOrEmpty(imageUrl))
                    continue;

                try
                {
                    // Extract username and other info from image URL
                    var imageUrlMatch = Regex.Match(imageUrl, @"https://[^/]+/([^/]+)/(\d+)/([^/]+)");
                    var username = imageUrlMatch.Success ? imageUrlMatch.Groups[1].Value : "Unknown";
                    var catalogId = imageUrlMatch.Success ? imageUrlMatch.Groups[2].Value : "Unknown";
                    var fileName = imageUrlMatch.Success ? imageUrlMatch.Groups[3].Value : "Unknown";

                    var nsfwImage = new NsfwImage
                    {
                        Url = imageUrl,
                        Source = SourceName,
                        Title = $"{fileName} (Album {catalogId})",
                        Artist = username,
                        Tags = "luscious,album"
                    };

                    results.Add(nsfwImage);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process individual image URL {ImageUrl}", imageUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process NSFW image results for {SourceName}", SourceName);
        }

        // Randomize results if requested
        if (randomize)
        {
            results = results.OrderBy(x => _random.Next()).ToList();
        }

        return results;
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
    }

    private string BuildFullUrl(string extractedUrl)
    {
        if (string.IsNullOrWhiteSpace(extractedUrl))
            return extractedUrl;

        // If the URL is already absolute (starts with http:// or https://), return as is
        if (extractedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            extractedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("URL is already absolute: {Url}", extractedUrl);
            return extractedUrl;
        }

        // For relative URLs, always append base URL if we have one (regardless of AppendBaseUrl setting)
        // This ensures picture page URLs like "/pictures/album/..." get proper base URL
        if (!string.IsNullOrEmpty(_options.BaseUrl) && extractedUrl.StartsWith("/"))
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var fullUrl = baseUrl + extractedUrl;
            _logger.LogDebug("Built full URL from relative: {RelativeUrl} -> {FullUrl}", extractedUrl, fullUrl);
            return fullUrl;
        }

        // If AppendBaseUrl is enabled and URL is relative without leading slash, append base URL
        if (_options.AppendBaseUrl && !string.IsNullOrEmpty(_options.BaseUrl))
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var relativeUrl = extractedUrl.StartsWith("/") ? extractedUrl : "/" + extractedUrl;
            var fullUrl = baseUrl + relativeUrl;
            _logger.LogDebug("Built full URL with AppendBaseUrl: {RelativeUrl} -> {FullUrl}", extractedUrl, fullUrl);
            return fullUrl;
        }

        // Return as-is if no base URL appending
        _logger.LogDebug("Using URL as-is: {Url}", extractedUrl);
        return extractedUrl;
    }

    private bool IsValidImageUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogDebug("URL validation failed: URL is null or whitespace");
            return false;
        }

        // Check if it's a valid URL
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogDebug("URL validation failed: Invalid URI format for {Url}", url);
            return false;
        }

        // Check if it has a valid scheme
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogDebug("URL validation failed: Invalid scheme {Scheme} for {Url}", uri.Scheme, url);
            return false;
        }

        // Check if it has an image file extension
        var path = uri.AbsolutePath.ToLowerInvariant();
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg" };

        var hasValidExtension = imageExtensions.Any(ext => path.EndsWith(ext));
        if (!hasValidExtension)
        {
            _logger.LogDebug("URL validation failed: No valid image extension for {Url}", url);
            return false;
        }

        // Additional validation for common issues
        if (url.Length > 2000) // Discord URL limit
        {
            _logger.LogDebug("URL validation failed: URL too long ({Length} chars) for {Url}", url.Length, url.Substring(0, 100) + "...");
            return false;
        }

        // Check for suspicious characters or patterns
        if (url.Contains(" ") || url.Contains("\n") || url.Contains("\r") || url.Contains("\t"))
        {
            _logger.LogDebug("URL validation failed: URL contains whitespace characters for {Url}", url);
            return false;
        }

        _logger.LogDebug("URL validation passed for {Url}", url);
        return true;
    }
}