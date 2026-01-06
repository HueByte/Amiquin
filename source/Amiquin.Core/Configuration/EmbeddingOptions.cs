namespace Amiquin.Core.Configuration;

/// <summary>
/// Configuration options for the embedding system.
/// Provider-agnostic - specific provider configuration is handled by the provider implementations.
/// </summary>
public class EmbeddingOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string Section = "Embedding";

    /// <summary>
    /// The provider to use for embeddings: "openai" or "ollama"
    /// </summary>
    public string Provider { get; set; } = "openai";

    /// <summary>
    /// Maximum text length before truncation
    /// </summary>
    public int MaxTextLength { get; set; } = 8000;

    /// <summary>
    /// Whether to log embedding generation metrics
    /// </summary>
    public bool LogMetrics { get; set; } = false;

    /// <summary>
    /// OpenAI-specific embedding configuration
    /// </summary>
    public OpenAIEmbeddingOptions OpenAI { get; set; } = new();

    /// <summary>
    /// Ollama-specific embedding configuration
    /// </summary>
    public OllamaEmbeddingOptions Ollama { get; set; } = new();
}

/// <summary>
/// Configuration options for OpenAI embedding provider.
/// </summary>
public class OpenAIEmbeddingOptions
{
    /// <summary>
    /// Model to use for embeddings (default: text-embedding-3-small)
    /// </summary>
    public string Model { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Embedding dimension (default: 1536 for text-embedding-3-small)
    /// </summary>
    public int Dimension { get; set; } = 1536;
}

/// <summary>
/// Configuration options for Ollama embedding provider.
/// </summary>
public class OllamaEmbeddingOptions
{
    /// <summary>
    /// Base URL for Ollama API (default: http://localhost:11434)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use for embeddings.
    /// Popular options: nomic-embed-text, mxbai-embed-large, all-minilm
    /// </summary>
    public string Model { get; set; } = "nomic-embed-text";

    /// <summary>
    /// Expected embedding dimension for the model.
    /// Common values: nomic-embed-text=768, mxbai-embed-large=1024, all-minilm=384
    /// </summary>
    public int Dimension { get; set; } = 768;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
