using Amiquin.Core.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;

namespace Amiquin.Core.Services.Embeddings;

/// <summary>
/// OpenAI-based embedding provider.
/// This is one possible implementation - the system is designed to be provider-agnostic.
/// </summary>
public class OpenAIEmbeddingProvider : IEmbeddingProvider
{
    private readonly ILogger<OpenAIEmbeddingProvider> _logger;
    private readonly OpenAIClient? _client;
    private readonly OpenAIEmbeddingOptions _options;

    public OpenAIEmbeddingProvider(
        ILogger<OpenAIEmbeddingProvider> logger,
        OpenAIClient? openAIClient,
        OpenAIEmbeddingOptions options)
    {
        _logger = logger;
        _client = openAIClient;
        _options = options;
    }

    public string ProviderId => "openai";

    public int EmbeddingDimension => _options.Dimension;

    public async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        if (_client == null)
        {
            _logger.LogWarning("OpenAI client not available for embeddings");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Cannot generate embedding for empty text");
            return null;
        }

        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_options.Model);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);

            _logger.LogDebug("Generated OpenAI embedding with {Dimension} dimensions using model {Model}",
                response.Value.ToFloats().Length, _options.Model);

            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate OpenAI embedding");
            return null;
        }
    }

    public async Task<List<float[]?>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
    {
        var textList = texts.ToList();
        if (_client == null || textList.Count == 0)
        {
            return textList.Select(_ => (float[]?)null).ToList();
        }

        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_options.Model);
            var response = await embeddingClient.GenerateEmbeddingsAsync(textList);

            _logger.LogDebug("Generated {Count} OpenAI embeddings using model {Model}",
                response.Value.Count, _options.Model);

            return response.Value.Select(e => (float[]?)e.ToFloats().ToArray()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate batch OpenAI embeddings");
            return textList.Select(_ => (float[]?)null).ToList();
        }
    }

    public Task<bool> IsAvailableAsync() => Task.FromResult(_client != null);
}
