namespace Amiquin.Core.Configuration;

/// <summary>
/// Configuration options for the conversation memory system
/// </summary>
public class MemoryOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string Section = "Memory";

    /// <summary>
    /// Qdrant connection configuration
    /// </summary>
    public QdrantOptions Qdrant { get; set; } = new();

    /// <summary>
    /// Whether memory system is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of memories to store per session
    /// </summary>
    public int MaxMemoriesPerSession { get; set; } = 1000;

    /// <summary>
    /// Maximum number of memories to retrieve for context
    /// </summary>
    public int MaxContextMemories { get; set; } = 10;

    /// <summary>
    /// Minimum similarity score for memory retrieval (0.0 to 1.0)
    /// </summary>
    public float SimilarityThreshold { get; set; } = 0.7f;

    /// <summary>
    /// Minimum importance score for memory creation (0.0 to 1.0)
    /// </summary>
    public float MinImportanceScore { get; set; } = 0.3f;

    /// <summary>
    /// Days after which old memories can be cleaned up
    /// </summary>
    public int MemoryRetentionDays { get; set; } = 30;

    /// <summary>
    /// Maximum importance score for cleanup (memories above this won't be deleted)
    /// </summary>
    public float CleanupMaxImportance { get; set; } = 0.8f;

    /// <summary>
    /// Minimum number of messages in a conversation before creating memories
    /// </summary>
    public int MinMessagesForMemory { get; set; } = 5;

    /// <summary>
    /// Interval in hours between automatic memory generation
    /// </summary>
    public int MemoryGenerationIntervalHours { get; set; } = 24;

    /// <summary>
    /// Whether to automatically clean up old memories
    /// </summary>
    public bool AutoCleanup { get; set; } = true;

    /// <summary>
    /// Interval in hours between automatic cleanup
    /// </summary>
    public int CleanupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Model to use for generating memory embeddings
    /// </summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Maximum token length for memory content
    /// </summary>
    public int MaxMemoryTokens { get; set; } = 500;

    /// <summary>
    /// Memory types and their importance multipliers
    /// </summary>
    public Dictionary<string, float> MemoryTypeImportance { get; set; } = new()
    {
        { "summary", 0.8f },
        { "fact", 0.9f },
        { "preference", 0.7f },
        { "context", 0.6f },
        { "emotion", 0.5f },
        { "event", 0.7f }
    };

    /// <summary>
    /// Session management configuration
    /// </summary>
    public SessionOptions Session { get; set; } = new();
}

/// <summary>
/// Configuration options for session management and auto-refresh
/// </summary>
public class SessionOptions
{
    /// <summary>
    /// Minutes of inactivity before session is considered stale and refreshed
    /// </summary>
    public int InactivityTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to enable automatic session refresh after inactivity
    /// </summary>
    public bool EnableAutoRefresh { get; set; } = true;

    /// <summary>
    /// Maximum messages in a session before auto-compaction is triggered
    /// </summary>
    public int MaxMessagesBeforeCompaction { get; set; } = 50;

    /// <summary>
    /// Number of recent messages to keep after compaction (others become memories)
    /// </summary>
    public int MessagesToKeepAfterCompaction { get; set; } = 10;

    /// <summary>
    /// Whether to extract memories from old messages during compaction
    /// </summary>
    public bool ExtractMemoriesOnCompaction { get; set; } = true;

    /// <summary>
    /// Maximum memories to inject when refreshing a stale session
    /// </summary>
    public int MaxMemoriesOnSessionRefresh { get; set; } = 5;

    /// <summary>
    /// Whether to create a summary memory when session is refreshed
    /// </summary>
    public bool CreateSummaryOnRefresh { get; set; } = true;
}

/// <summary>
/// Qdrant-specific configuration options
/// </summary>
public class QdrantOptions
{
    /// <summary>
    /// Qdrant server host
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Qdrant server port
    /// </summary>
    public int Port { get; set; } = 6334;

    /// <summary>
    /// Use HTTPS for connection
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// API key for authentication (if required)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Collection name for storing memories
    /// </summary>
    public string CollectionName { get; set; } = "amiquin_memories";

    /// <summary>
    /// Vector dimension (should match embedding model)
    /// </summary>
    public uint VectorSize { get; set; } = 1536; // text-embedding-3-small dimension

    /// <summary>
    /// Distance metric for vector similarity
    /// </summary>
    public string Distance { get; set; } = "Cosine"; // Cosine, Euclid, Dot

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retries for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to create collection if it doesn't exist
    /// </summary>
    public bool AutoCreateCollection { get; set; } = true;
}