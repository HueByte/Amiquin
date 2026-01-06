using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amiquin.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Embeddings;

/// <summary>
/// Ollama-based embedding provider for local model support.
/// Connects to a locally running Ollama instance for embedding generation.
/// </summary>
public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly ILogger<OllamaEmbeddingProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly OllamaEmbeddingOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaEmbeddingProvider(
        ILogger<OllamaEmbeddingProvider> logger,
        HttpClient httpClient,
        OllamaEmbeddingOptions options)
    {
        _logger = logger;
        _httpClient = httpClient;
        _options = options;
    }

    public string ProviderId => "ollama";

    public int EmbeddingDimension => _options.Dimension;

    private string BaseUrl => _options.BaseUrl.TrimEnd('/');

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot generate embedding for empty text");
            return null;
        }

        try
        {
            var request = new OllamaEmbeddingRequest
            {
                Model = _options.Model,
                Prompt = text
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/embeddings",
                request,
                JsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama embedding request failed with status {StatusCode}: {Error}",
                    response.StatusCode, errorContent);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(JsonOptions);

            if (result?.Embedding == null || result.Embedding.Length == 0)
            {
                _logger.LogWarning("Ollama returned empty embedding for model {Model}", _options.Model);
                return null;
            }

            _logger.LogDebug("Generated Ollama embedding with {Dimension} dimensions using model {Model}",
                result.Embedding.Length, _options.Model);

            return result.Embedding;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama at {BaseUrl}", BaseUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Ollama embedding");
            return null;
        }
    }

    public async Task<List<float[]?>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        var results = new List<float[]?>(textList.Count);

        // Ollama doesn't have native batch embedding, so we process sequentially
        // Could be parallelized if needed, but sequential is safer for local resources
        foreach (var text in textList)
        {
            var embedding = await GenerateEmbeddingAsync(text);
            results.Add(embedding);
        }

        return results;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/tags");
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            // Check if the required model is available
            var content = await response.Content.ReadAsStringAsync();
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(content, JsonOptions);

            var modelAvailable = tagsResponse?.Models?.Any(m =>
                m.Name?.StartsWith(_options.Model, StringComparison.OrdinalIgnoreCase) == true) ?? false;

            if (!modelAvailable)
            {
                _logger.LogWarning("Ollama model '{Model}' not found. Available models: {Models}",
                    _options.Model,
                    string.Join(", ", tagsResponse?.Models?.Select(m => m.Name) ?? []));
            }

            return modelAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ollama health check failed");
            return false;
        }
    }

    private class OllamaEmbeddingRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
    }

    private class OllamaEmbeddingResponse
    {
        public float[]? Embedding { get; set; }
    }

    private class OllamaTagsResponse
    {
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        public string? Name { get; set; }
    }
}
