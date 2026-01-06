namespace Amiquin.Core.Services.WebSearch;

/// <summary>
/// Service for performing web searches to retrieve real-time information.
/// </summary>
public interface IWebSearchService
{
    /// <summary>
    /// Performs a web search and returns relevant results.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results to return (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results containing title, snippet, and URL.</returns>
    Task<WebSearchResult> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a web search operation.
/// </summary>
public class WebSearchResult
{
    /// <summary>
    /// List of search results.
    /// </summary>
    public List<WebSearchItem> Items { get; set; } = new();

    /// <summary>
    /// The original search query.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Whether the search was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the search failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a single search result item.
/// </summary>
public class WebSearchItem
{
    /// <summary>
    /// Title of the search result.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Snippet/description of the search result.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;

    /// <summary>
    /// URL of the search result.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}
