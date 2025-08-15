namespace Amiquin.Core.Services.Scrappers;

/// <summary>
/// Interface for scrapper implementations
/// </summary>
public interface IScrapper
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
    /// Scrapes and returns multiple results of type T
    /// </summary>
    /// <typeparam name="T">The type to return</typeparam>
    /// <param name="count">Number of results to return (default: 5)</param>
    /// <returns>List of scraped results</returns>
    Task<List<T>> ScrapeAsync<T>(int count = 5) where T : class;

    /// <summary>
    /// Processes raw extracted data into results
    /// </summary>
    /// <typeparam name="T">The type to return</typeparam>
    /// <param name="extractedData">The raw extracted data</param>
    /// <param name="count">Number of results to return</param>
    /// <returns>List of processed results</returns>
    List<T> ProcessResults<T>(List<string> extractedData, int count = 5) where T : class;

    /// <summary>
    /// Returns raw image URLs from the scrapper
    /// </summary>
    /// <param name="count">Number of URLs to return (default: 5)</param>
    /// <returns>Array of image URLs</returns>
    Task<string[]> GetImageUrlsAsync(int count = 5);
}