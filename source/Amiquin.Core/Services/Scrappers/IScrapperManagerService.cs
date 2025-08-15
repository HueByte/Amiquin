namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Interface for managing multiple scrapper providers
/// </summary>
public interface IScrapperManagerService
{
    /// <summary>
    /// Gets all available image scrapers
    /// </summary>
    IEnumerable<IImageScraper> GetImageScrapers();

    /// <summary>
    /// Gets all available data scrapers
    /// </summary>
    IEnumerable<IDataScrapper> GetDataScrapers();

    /// <summary>
    /// Scrapes images from multiple providers for gallery display
    /// Implementation should pick 5 random albums and extract random images from those albums
    /// </summary>
    /// <param name="count">Total number of image URLs to return</param>
    /// <param name="randomize">Whether to randomize the order of URLs</param>
    /// <param name="useCache">Whether to use cached results if available</param>
    /// <returns>Array of image URLs from multiple providers</returns>
    Task<string[]> ScrapeGalleryImagesAsync(int count = 10, bool randomize = true, bool useCache = true);

    /// <summary>
    /// Gets a specific image scraper by source name
    /// </summary>
    /// <param name="sourceName">The name of the scraper source</param>
    /// <returns>The image scraper instance or null if not found</returns>
    IImageScraper? GetImageScraper(string sourceName);

    /// <summary>
    /// Clears cache for all scrapers
    /// </summary>
    void ClearAllCaches();
}