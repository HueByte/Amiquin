using Qdrant.Client.Grpc;

namespace Amiquin.Core.Models;

/// <summary>
/// Represents a memory stored in Qdrant vector database
/// </summary>
public class QdrantMemory
{
    /// <summary>
    /// Unique identifier for the memory
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Chat session ID this memory belongs to
    /// </summary>
    public string ChatSessionId { get; set; } = string.Empty;

    /// <summary>
    /// The content/text of the memory
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of memory (summary, fact, preference, context, etc.)
    /// </summary>
    public string MemoryType { get; set; } = "context";

    /// <summary>
    /// Importance score of this memory (0.0 to 1.0)
    /// </summary>
    public float ImportanceScore { get; set; } = 0.5f;

    /// <summary>
    /// When this memory was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this memory was accessed/retrieved
    /// </summary>
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this memory has been retrieved
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// Associated Discord User ID if this memory is user-specific
    /// </summary>
    public ulong? UserId { get; set; }

    /// <summary>
    /// Estimated token count for this memory content
    /// </summary>
    public int EstimatedTokens { get; set; } = 0;

    /// <summary>
    /// Additional metadata as key-value pairs
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Updates access tracking information
    /// </summary>
    public void MarkAccessed()
    {
        LastAccessedAt = DateTime.UtcNow;
        AccessCount++;
    }

    /// <summary>
    /// Calculates relevance score based on importance, recency, and access frequency
    /// </summary>
    public float CalculateRelevanceScore()
    {
        var daysSinceCreated = (DateTime.UtcNow - CreatedAt).TotalDays;
        var daysSinceAccessed = (DateTime.UtcNow - LastAccessedAt).TotalDays;
        
        // Importance weighted by recency and access frequency
        var recencyFactor = Math.Max(0.1f, 1.0f - (float)(daysSinceAccessed / 30.0)); // Decay over 30 days
        var frequencyFactor = Math.Min(1.0f, AccessCount / 10.0f); // Max benefit at 10 accesses
        
        return ImportanceScore * recencyFactor * (1.0f + frequencyFactor * 0.2f);
    }

    /// <summary>
    /// Converts this memory to a Qdrant point for storage
    /// </summary>
    public PointStruct ToQdrantPoint(float[] embedding)
    {
        var payload = new Dictionary<string, Value>
        {
            ["chatSessionId"] = new Value { StringValue = ChatSessionId },
            ["content"] = new Value { StringValue = Content },
            ["memoryType"] = new Value { StringValue = MemoryType },
            ["importanceScore"] = new Value { DoubleValue = ImportanceScore },
            ["createdAt"] = new Value { StringValue = CreatedAt.ToString("O") },
            ["lastAccessedAt"] = new Value { StringValue = LastAccessedAt.ToString("O") },
            ["accessCount"] = new Value { IntegerValue = AccessCount },
            ["estimatedTokens"] = new Value { IntegerValue = EstimatedTokens }
        };

        if (UserId.HasValue)
        {
            payload["userId"] = new Value { StringValue = UserId.Value.ToString() };
        }

        // Add custom metadata
        foreach (var kvp in Metadata)
        {
            payload[$"meta_{kvp.Key}"] = new Value { StringValue = kvp.Value };
        }

        return new PointStruct
        {
            Id = new PointId { Uuid = Id },
            Vectors = new Vectors { Vector = new Vector { Data = { embedding } } },
            Payload = { payload }
        };
    }

    /// <summary>
    /// Creates a QdrantMemory from a Qdrant retrieved point
    /// </summary>
    public static QdrantMemory FromQdrantPoint(RetrievedPoint point)
    {
        var memory = new QdrantMemory
        {
            Id = point.Id.Uuid
        };

        var payload = point.Payload;

        if (payload.TryGetValue("chatSessionId", out var sessionId))
            memory.ChatSessionId = sessionId.StringValue;

        if (payload.TryGetValue("content", out var content))
            memory.Content = content.StringValue;

        if (payload.TryGetValue("memoryType", out var memoryType))
            memory.MemoryType = memoryType.StringValue;

        if (payload.TryGetValue("importanceScore", out var importance))
            memory.ImportanceScore = (float)importance.DoubleValue;

        if (payload.TryGetValue("createdAt", out var createdAt) && 
            DateTime.TryParse(createdAt.StringValue, out var created))
            memory.CreatedAt = created;

        if (payload.TryGetValue("lastAccessedAt", out var lastAccessed) && 
            DateTime.TryParse(lastAccessed.StringValue, out var accessed))
            memory.LastAccessedAt = accessed;

        if (payload.TryGetValue("accessCount", out var accessCount))
            memory.AccessCount = (int)accessCount.IntegerValue;

        if (payload.TryGetValue("userId", out var userId) && 
            ulong.TryParse(userId.StringValue, out var parsedUserId))
            memory.UserId = parsedUserId;

        if (payload.TryGetValue("estimatedTokens", out var tokens))
            memory.EstimatedTokens = (int)tokens.IntegerValue;

        // Extract custom metadata
        foreach (var kvp in payload.Where(p => p.Key.StartsWith("meta_")))
        {
            var key = kvp.Key.Substring(5); // Remove "meta_" prefix
            memory.Metadata[key] = kvp.Value.StringValue;
        }

        return memory;
    }

    /// <summary>
    /// Creates a QdrantMemory from a Qdrant scored point (includes similarity score)
    /// </summary>
    public static (QdrantMemory Memory, float Score) FromQdrantScoredPoint(ScoredPoint point)
    {
        var memory = new QdrantMemory
        {
            Id = point.Id.Uuid
        };

        var payload = point.Payload;

        if (payload.TryGetValue("chatSessionId", out var sessionId))
            memory.ChatSessionId = sessionId.StringValue;

        if (payload.TryGetValue("content", out var content))
            memory.Content = content.StringValue;

        if (payload.TryGetValue("memoryType", out var memoryType))
            memory.MemoryType = memoryType.StringValue;

        if (payload.TryGetValue("importanceScore", out var importance))
            memory.ImportanceScore = (float)importance.DoubleValue;

        if (payload.TryGetValue("createdAt", out var createdAt) && 
            DateTime.TryParse(createdAt.StringValue, out var created))
            memory.CreatedAt = created;

        if (payload.TryGetValue("lastAccessedAt", out var lastAccessed) && 
            DateTime.TryParse(lastAccessed.StringValue, out var accessed))
            memory.LastAccessedAt = accessed;

        if (payload.TryGetValue("accessCount", out var accessCount))
            memory.AccessCount = (int)accessCount.IntegerValue;

        if (payload.TryGetValue("userId", out var userId) && 
            ulong.TryParse(userId.StringValue, out var parsedUserId))
            memory.UserId = parsedUserId;

        if (payload.TryGetValue("estimatedTokens", out var tokens))
            memory.EstimatedTokens = (int)tokens.IntegerValue;

        // Extract custom metadata
        foreach (var kvp in payload.Where(p => p.Key.StartsWith("meta_")))
        {
            var key = kvp.Key.Substring(5); // Remove "meta_" prefix
            memory.Metadata[key] = kvp.Value.StringValue;
        }

        return (memory, point.Score);
    }
}