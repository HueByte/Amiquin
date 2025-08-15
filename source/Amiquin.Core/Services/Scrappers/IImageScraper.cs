namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Interface for image scrapping implementations
/// </summary>
public interface IImageScraper
{
    /// <summary>
    /// The unique name of the scrapper
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Whether the scrapper is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Scrapes and returns image URLs
    /// </summary>
    /// <param name="count">Number of image URLs to return</param>
    /// <param name="randomize">Whether to randomize the order of URLs</param>
    /// <param name="useCache">Whether to use cached results if available</param>
    /// <returns>Array of image URLs</returns>
    Task<string[]> ScrapeImagesUrlsAsync(int count = 5, bool randomize = true, bool useCache = true);

    /// <summary>
    /// Validates if the provided URLs are valid image URLs
    /// </summary>
    /// <param name="urls">URLs to validate</param>
    /// <returns>Array of valid image URLs</returns>
    Task<string[]> ValidateImageUrlsAsync(string[] urls);

    /// <summary>
    /// Clears the cache for this scrapper
    /// </summary>
    void ClearCache();
}