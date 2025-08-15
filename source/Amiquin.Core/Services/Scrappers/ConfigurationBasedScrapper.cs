using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Scrappers.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Configuration-based scrapper that uses ScrapperStep for extraction
/// </summary>
public class ConfigurationBasedScrapper : IScrapper
{
    private readonly ILogger<ConfigurationBasedScrapper> _logger;
    private readonly HttpClient _httpClient;
    private readonly ScrapperProviderOptions _options;
    private readonly Random _random;

    public ConfigurationBasedScrapper(
        ILogger<ConfigurationBasedScrapper> logger,
        HttpClient httpClient,
        ScrapperProviderOptions options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options;
        _random = new Random();

        ConfigureHttpClient();
    }

    public string SourceName => _options.SourceName;
    public bool IsEnabled => _options.Enabled;

    public async Task<List<T>> ScrapeAsync<T>(int count = 5) where T : class
    {
        try
        {
            var extractedData = await ExecuteScrapingStepsAsync(count);
            return ProcessResults<T>(extractedData, count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape from {SourceName}", SourceName);
            return new List<T>();
        }
    }

    public List<T> ProcessResults<T>(List<string> extractedData, int count = 5) where T : class
    {
        if (typeof(T) == typeof(NsfwImage))
        {
            return ProcessNsfwImageResults(extractedData, count).Cast<T>().ToList();
        }

        _logger.LogWarning("Unsupported result type {Type} for {SourceName}", typeof(T).Name, SourceName);
        return new List<T>();
    }

    public async Task<string[]> GetImageUrlsAsync(int count = 5)
    {
        try
        {
            var extractedData = await ExecuteScrapingStepsAsync(count);
            // Return the final extracted data as image URLs
            return extractedData.Take(count).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get image URLs from {SourceName}", SourceName);
            return Array.Empty<string>();
        }
    }

    private async Task<List<string>> ExecuteScrapingStepsAsync(int count = 5)
    {
        _logger.LogInformation("Starting scraping process for {SourceName} with {Count} results requested", SourceName, count);
        _logger.LogInformation("Using {StepCount} extraction steps", _options.ExtractionSteps.Count);
        _logger.LogDebug("Extraction steps: {Steps}", string.Join(", ", _options.ExtractionSteps.Select(s =>
            !string.IsNullOrEmpty(s.Name) ? $"{s.Name}: {s.Url}" : s.Url)));
        var currentData = new List<string>();

        foreach (var step in _options.ExtractionSteps)
        {
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

    private List<NsfwImage> ProcessNsfwImageResults(List<string> extractedData, int count = 5)
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

        return results;
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
    }
}