using Amiquin.Core.Options;
using Amiquin.Core.Utilities;
using Amiquin.Core.Utilities.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace Amiquin.Core.Services.WebSearch;

/// <summary>
/// Implementation of web search service supporting multiple providers.
/// </summary>
public class WebSearchService : IWebSearchService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<WebSearchService> _logger;
    private readonly WebSearchOptions _options;

    public WebSearchService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<WebSearchService> logger,
        IOptions<WebSearchOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<WebSearchResult> SearchAsync(string query, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Web search is disabled in configuration");
            return new WebSearchResult
            {
                Query = query,
                Success = false,
                ErrorMessage = "Web search is disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Empty search query provided");
            return new WebSearchResult
            {
                Query = query,
                Success = false,
                ErrorMessage = "Empty search query"
            };
        }

        // Check cache first
        if (_options.EnableCaching)
        {
            var cacheKey = StringModifier.CreateCacheKey("WebSearch", query);
            if (_memoryCache.TryGetTypedValue(cacheKey, out WebSearchResult? cachedResult) && cachedResult != null)
            {
                _logger.LogDebug("Returning cached search results for query: {Query}", query);
                return cachedResult;
            }
        }

        try
        {
            WebSearchResult result = _options.Provider.ToLowerInvariant() switch
            {
                "google" => await SearchGoogleAsync(query, maxResults, cancellationToken),
                "bing" => await SearchBingAsync(query, maxResults, cancellationToken),
                "duckduckgo" or "ddg" => await SearchDuckDuckGoAsync(query, maxResults, cancellationToken),
                _ => throw new NotSupportedException($"Search provider '{_options.Provider}' is not supported")
            };

            // Cache successful results
            if (result.Success && _options.EnableCaching)
            {
                var cacheKey = StringModifier.CreateCacheKey("WebSearch", query);
                _memoryCache.SetAbsolute(cacheKey, result, TimeSpan.FromMinutes(_options.CacheExpirationMinutes));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing web search for query: {Query}", query);
            return new WebSearchResult
            {
                Query = query,
                Success = false,
                ErrorMessage = $"Search failed: {ex.Message}"
            };
        }
    }

    private async Task<WebSearchResult> SearchGoogleAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.SearchEngineId))
        {
            throw new InvalidOperationException("Google Custom Search requires ApiKey and SearchEngineId");
        }

        var httpClient = _httpClientFactory.CreateClient("WebSearch");
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"https://www.googleapis.com/customsearch/v1?key={_options.ApiKey}&cx={_options.SearchEngineId}&q={encodedQuery}&num={maxResults}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var result = new WebSearchResult
        {
            Query = query,
            Success = true
        };

        if (json.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                result.Items.Add(new WebSearchItem
                {
                    Title = item.GetProperty("title").GetString() ?? string.Empty,
                    Snippet = item.GetProperty("snippet").GetString() ?? string.Empty,
                    Url = item.GetProperty("link").GetString() ?? string.Empty
                });
            }
        }

        _logger.LogInformation("Google search completed for query '{Query}', found {Count} results", query, result.Items.Count);
        return result;
    }

    private async Task<WebSearchResult> SearchBingAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Bing Search requires ApiKey");
        }

        var httpClient = _httpClientFactory.CreateClient("WebSearch");
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _options.ApiKey);

        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"https://api.bing.microsoft.com/v7.0/search?q={encodedQuery}&count={maxResults}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var result = new WebSearchResult
        {
            Query = query,
            Success = true
        };

        if (json.TryGetProperty("webPages", out var webPages) && webPages.TryGetProperty("value", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                result.Items.Add(new WebSearchItem
                {
                    Title = item.GetProperty("name").GetString() ?? string.Empty,
                    Snippet = item.GetProperty("snippet").GetString() ?? string.Empty,
                    Url = item.GetProperty("url").GetString() ?? string.Empty
                });
            }
        }

        _logger.LogInformation("Bing search completed for query '{Query}', found {Count} results", query, result.Items.Count);
        return result;
    }

    private async Task<WebSearchResult> SearchDuckDuckGoAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        // DuckDuckGo Instant Answer API (no API key required, but limited results)
        var httpClient = _httpClientFactory.CreateClient("WebSearch");
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"https://api.duckduckgo.com/?q={encodedQuery}&format=json&no_html=1&skip_disambig=1";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        var result = new WebSearchResult
        {
            Query = query,
            Success = true
        };

        // Add abstract (main result)
        if (json.TryGetProperty("Abstract", out var abstractProp) && !string.IsNullOrWhiteSpace(abstractProp.GetString()))
        {
            result.Items.Add(new WebSearchItem
            {
                Title = json.GetProperty("Heading").GetString() ?? query,
                Snippet = abstractProp.GetString() ?? string.Empty,
                Url = json.GetProperty("AbstractURL").GetString() ?? string.Empty
            });
        }

        // Add related topics
        if (json.TryGetProperty("RelatedTopics", out var topics))
        {
            foreach (var topic in topics.EnumerateArray().Take(maxResults - result.Items.Count))
            {
                if (topic.TryGetProperty("Text", out var text) && topic.TryGetProperty("FirstURL", out var topicUrl))
                {
                    var topicText = text.GetString();
                    if (!string.IsNullOrWhiteSpace(topicText))
                    {
                        // Extract title (usually before the first " - ")
                        var parts = topicText.Split(" - ", 2);
                        result.Items.Add(new WebSearchItem
                        {
                            Title = parts[0].Trim(),
                            Snippet = parts.Length > 1 ? parts[1].Trim() : topicText,
                            Url = topicUrl.GetString() ?? string.Empty
                        });
                    }
                }
            }
        }

        _logger.LogInformation("DuckDuckGo search completed for query '{Query}', found {Count} results", query, result.Items.Count);
        return result;
    }
}
