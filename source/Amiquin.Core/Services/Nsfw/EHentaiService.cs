using Amiquin.Core.Models.EHentai;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Amiquin.Core.Services.Nsfw;

/// <summary>
/// Service for scraping e-hentai gallery links and fetching gallery metadata via API
/// </summary>
public class EHentaiService
{
    private readonly ILogger<EHentaiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    private const string BaseUrl = "https://e-hentai.org";
    private const string ApiUrl = "https://api.e-hentai.org/api.php";
    private const string SearchUrl = "https://e-hentai.org/?f_cats=32&f_search=hentai";

    // Regex pattern to match gallery URLs: https://e-hentai.org/g/{gallery_id}/{gallery_token}/
    private static readonly Regex GalleryUrlRegex = new(
        @"https://e-hentai\.org/g/(\d+)/([a-f0-9]+)/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex pattern to extract image URLs from gallery pages
    private static readonly Regex ImageUrlRegex = new(
        @"background:\s*url\(&quot;([^&]+)&quot;\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public EHentaiService(ILogger<EHentaiService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;

        // Set user agent to avoid being blocked
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    /// <summary>
    /// Scrapes e-hentai search page to extract gallery links
    /// </summary>
    /// <param name="searchQuery">Search query (defaults to "hentai")</param>
    /// <param name="maxPages">Maximum number of pages to scrape (defaults to 1)</param>
    /// <returns>List of extracted gallery links</returns>
    public async Task<List<EHentaiGalleryLink>> ScrapeGalleryLinksAsync(string searchQuery = "hentai", int maxPages = 1)
    {
        var galleryLinks = new List<EHentaiGalleryLink>();

        try
        {
            for (int page = 0; page < maxPages; page++)
            {
                var searchUrl = $"{BaseUrl}/?f_cats=32&f_search={Uri.EscapeDataString(searchQuery)}";
                if (page > 0)
                {
                    searchUrl += $"&page={page}";
                }

                _logger.LogInformation("Scraping e-hentai search page: {Url}", searchUrl);

                var response = await _httpClient.GetStringAsync(searchUrl);
                var pageLinks = ExtractGalleryLinksFromHtml(response);

                galleryLinks.AddRange(pageLinks);

                _logger.LogInformation("Extracted {Count} gallery links from page {Page}", pageLinks.Count, page);

                // Add delay between pages to be respectful
                if (page < maxPages - 1)
                {
                    await Task.Delay(2000);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping e-hentai gallery links");
        }

        return galleryLinks.GroupBy(g => g.GalleryId).Select(g => g.First()).ToList(); // Remove duplicates
    }

    /// <summary>
    /// Extracts gallery links from HTML content using regex
    /// </summary>
    /// <param name="html">HTML content to parse</param>
    /// <returns>List of extracted gallery links</returns>
    private List<EHentaiGalleryLink> ExtractGalleryLinksFromHtml(string html)
    {
        var links = new List<EHentaiGalleryLink>();

        var matches = GalleryUrlRegex.Matches(html);
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var galleryId = match.Groups[1].Value;
                var galleryToken = match.Groups[2].Value;
                var url = match.Value;

                links.Add(new EHentaiGalleryLink
                {
                    GalleryId = galleryId,
                    GalleryToken = galleryToken,
                    Url = url
                });
            }
        }

        return links;
    }

    /// <summary>
    /// Fetches gallery metadata from e-hentai API
    /// </summary>
    /// <param name="galleryLinks">List of gallery links to fetch metadata for</param>
    /// <returns>List of gallery metadata</returns>
    public async Task<List<EHentaiGalleryMetadata>> FetchGalleryMetadataAsync(List<EHentaiGalleryLink> galleryLinks)
    {
        var allMetadata = new List<EHentaiGalleryMetadata>();

        // Process in batches of 25 (API limit)
        const int batchSize = 25;
        for (int i = 0; i < galleryLinks.Count; i += batchSize)
        {
            var batch = galleryLinks.Skip(i).Take(batchSize).ToList();

            try
            {
                var batchMetadata = await FetchBatchMetadataAsync(batch);
                allMetadata.AddRange(batchMetadata);

                _logger.LogInformation("Fetched metadata for {Count} galleries in batch {BatchNumber}",
                    batchMetadata.Count, (i / batchSize) + 1);

                // Add delay between batches as recommended by API docs
                if (i + batchSize < galleryLinks.Count)
                {
                    await Task.Delay(5000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching batch metadata for galleries {StartIndex}-{EndIndex}",
                    i, Math.Min(i + batchSize - 1, galleryLinks.Count - 1));
            }
        }

        return allMetadata;
    }

    /// <summary>
    /// Fetches metadata for a batch of galleries using the e-hentai API
    /// </summary>
    /// <param name="galleryLinks">Batch of gallery links (max 25)</param>
    /// <returns>List of gallery metadata</returns>
    private async Task<List<EHentaiGalleryMetadata>> FetchBatchMetadataAsync(List<EHentaiGalleryLink> galleryLinks)
    {
        var request = new EHentaiApiRequest
        {
            Method = "gdata",
            Namespace = 1,
            GalleryList = galleryLinks.Select(link => new List<object>
            {
                long.Parse(link.GalleryId),
                link.GalleryToken
            }).ToList()
        };

        var requestJson = JsonSerializer.Serialize(request);
        _logger.LogDebug("Making e-hentai API request: {Request}", requestJson);

        var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(ApiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("e-hentai API returned {StatusCode}: {ReasonPhrase}",
                response.StatusCode, response.ReasonPhrase);
            return new List<EHentaiGalleryMetadata>();
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("e-hentai API response: {Response}", responseContent);

        var apiResponse = JsonSerializer.Deserialize<EHentaiApiResponse>(responseContent);
        return apiResponse?.GalleryMetadata ?? new List<EHentaiGalleryMetadata>();
    }

    /// <summary>
    /// Scrapes direct image URLs from a gallery page
    /// </summary>
    /// <param name="galleryUrl">URL of the gallery page</param>
    /// <returns>List of direct image URLs</returns>
    public async Task<List<string>> ScrapeGalleryImageUrlsAsync(string galleryUrl)
    {
        var imageUrls = new List<string>();

        try
        {
            _logger.LogInformation("Scraping image URLs from gallery: {Url}", galleryUrl);

            var response = await _httpClient.GetStringAsync(galleryUrl);

            // Extract image URLs from the background style attributes
            var matches = ImageUrlRegex.Matches(response);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 2)
                {
                    var imageUrl = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("http"))
                    {
                        imageUrls.Add(imageUrl);
                    }
                }
            }

            _logger.LogInformation("Extracted {Count} image URLs from gallery", imageUrls.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scraping image URLs from gallery: {Url}", galleryUrl);
        }

        return imageUrls.Distinct().ToList(); // Remove duplicates
    }

    /// <summary>
    /// Gets random direct image URLs by scraping gallery catalogs and pages
    /// </summary>
    /// <param name="count">Number of random images to fetch</param>
    /// <param name="searchQuery">Search query to use</param>
    /// <returns>List of direct image URLs</returns>
    public async Task<List<string>> GetRandomImageUrlsAsync(int count = 5, string searchQuery = "hentai")
    {
        try
        {
            // First scrape gallery links
            var galleryLinks = await ScrapeGalleryLinksAsync(searchQuery, 1);

            if (galleryLinks.Count == 0)
            {
                _logger.LogWarning("No gallery links found for search query: {Query}", searchQuery);
                return new List<string>();
            }

            // Take a few random galleries and scrape their image URLs
            var randomGalleries = galleryLinks.OrderBy(x => _random.Next()).Take(Math.Min(3, galleryLinks.Count)).ToList();
            var allImageUrls = new List<string>();

            foreach (var gallery in randomGalleries)
            {
                var imageUrls = await ScrapeGalleryImageUrlsAsync(gallery.Url);
                allImageUrls.AddRange(imageUrls);

                // Add delay between gallery scrapes
                await Task.Delay(2000);

                // Break early if we have enough images
                if (allImageUrls.Count >= count * 2) break;
            }

            // Return random selection of the requested count
            return allImageUrls.OrderBy(x => _random.Next()).Take(count).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random e-hentai image URLs");
            return new List<string>();
        }
    }

    /// <summary>
    /// Gets random gallery metadata by scraping and fetching from API
    /// </summary>
    /// <param name="count">Number of random galleries to fetch</param>
    /// <param name="searchQuery">Search query to use</param>
    /// <returns>List of random gallery metadata</returns>
    public async Task<List<EHentaiGalleryMetadata>> GetRandomGalleriesAsync(int count = 5, string searchQuery = "hentai")
    {
        try
        {
            // Scrape multiple pages to get a larger pool for randomization
            var pages = Math.Max(1, (count / 25) + 1); // Ensure we have enough galleries
            var galleryLinks = await ScrapeGalleryLinksAsync(searchQuery, pages);

            if (galleryLinks.Count == 0)
            {
                _logger.LogWarning("No gallery links found for search query: {Query}", searchQuery);
                return new List<EHentaiGalleryMetadata>();
            }

            // Randomize and take the requested count
            var randomLinks = galleryLinks.OrderBy(x => _random.Next()).Take(count).ToList();

            // Fetch metadata for the random selection
            var metadata = await FetchGalleryMetadataAsync(randomLinks);

            return metadata.Take(count).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting random e-hentai galleries");
            return new List<EHentaiGalleryMetadata>();
        }
    }
}