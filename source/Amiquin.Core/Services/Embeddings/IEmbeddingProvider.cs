namespace Amiquin.Core.Services.Embeddings;

/// <summary>
/// Model-agnostic interface for generating text embeddings.
/// Implementations can use any embedding source: cloud APIs, local models, or custom solutions.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Unique identifier for this provider instance
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// The dimension of the embedding vectors produced by this provider
    /// </summary>
    int EmbeddingDimension { get; }

    /// <summary>
    /// Generates an embedding vector for the given text
    /// </summary>
    /// <param name="text">The text to generate an embedding for</param>
    /// <returns>The embedding vector as a float array, or null if generation failed</returns>
    Task<float[]?> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Generates embedding vectors for multiple texts in a batch
    /// </summary>
    /// <param name="texts">The texts to generate embeddings for</param>
    /// <returns>List of embedding vectors, with null entries for failed generations</returns>
    Task<List<float[]?>> GenerateEmbeddingsAsync(IEnumerable<string> texts);

    /// <summary>
    /// Validates if the provider is properly configured and available
    /// </summary>
    Task<bool> IsAvailableAsync();
}

/// <summary>
/// Response containing embedding generation results and metadata
/// </summary>
public class EmbeddingResult
{
    /// <summary>
    /// The generated embedding vector
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Whether the generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of tokens in the input text (if available)
    /// </summary>
    public int? TokenCount { get; set; }

    public static EmbeddingResult FromEmbedding(float[] embedding, int? tokenCount = null) => new()
    {
        Embedding = embedding,
        Success = true,
        TokenCount = tokenCount
    };

    public static EmbeddingResult FromError(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
