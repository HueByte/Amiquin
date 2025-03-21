using System.Net.Http.Json;
using Amiquin.Core.Services.ApiClients.Responses;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ApiClients;

public class NewsApiClient : INewsApiClient
{
    private readonly ILogger<NewsApiClient> _logger;
    private readonly HttpClient _httpClient;

    public NewsApiClient(ILogger<NewsApiClient> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(typeof(INewsApiClient).Name);
    }

    public async Task<NewsApiResponse?> GetNewsAsync()
    {
        var relativeUri = $"api/en/news?category={Constants.APIs.NewsApi.Category}&max_limit={Constants.APIs.NewsApi.MaxLimit}&include_card_data={Constants.APIs.NewsApi.IncludeCard.ToString().ToLower()}";

        var response = await _httpClient.GetAsync(relativeUri);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            return null;
        }

        var content = await response.Content.ReadFromJsonAsync<NewsApiResponse>();
        return content;
    }
}