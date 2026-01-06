namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for web search functionality.
/// </summary>
public class WebSearchOptions
{
    public const string SectionName = "WebSearch";

    /// <summary>
    /// Whether web search is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The search provider to use (Google, Bing, DuckDuckGo).
    /// </summary>
    public string Provider { get; set; } = "DuckDuckGo";

    /// <summary>
    /// API key for the search provider (if required).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Custom search engine ID (for Google Custom Search).
    /// </summary>
    public string? SearchEngineId { get; set; }

    /// <summary>
    /// Maximum number of search results to return per query.
    /// </summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>
    /// Timeout for search requests in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether to cache search results.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes.
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 30;
}
