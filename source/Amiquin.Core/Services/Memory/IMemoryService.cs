using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Memory;

/// <summary>
/// Service interface for managing conversation memories and vector operations
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Creates a new memory from conversation content
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="content">Memory content</param>
    /// <param name="memoryType">Type of memory (summary, fact, preference, etc.)</param>
    /// <param name="userId">Optional user ID if memory is user-specific</param>
    /// <param name="importance">Optional importance score override</param>
    /// <returns>Created memory</returns>
    Task<QdrantMemory> CreateMemoryAsync(
        string sessionId, 
        string content, 
        string memoryType = "context", 
        ulong? userId = null, 
        float? importance = null);

    /// <summary>
    /// Retrieves relevant memories for a given query
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="query">Query text to find similar memories</param>
    /// <param name="maxResults">Maximum number of memories to return</param>
    /// <returns>List of relevant memories with similarity scores</returns>
    Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetRelevantMemoriesAsync(
        string sessionId, 
        string query, 
        int maxResults = 5);

    /// <summary>
    /// Analyzes recent conversation and extracts memories
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="messages">Recent conversation messages</param>
    /// <returns>List of created memories</returns>
    Task<List<QdrantMemory>> ExtractMemoriesFromConversationAsync(
        string sessionId, 
        List<SessionMessage> messages);

    /// <summary>
    /// Gets formatted memory context for injection into chat
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="currentQuery">Current user query</param>
    /// <returns>Formatted memory context string</returns>
    Task<string?> GetMemoryContextAsync(string sessionId, string? currentQuery = null);

    /// <summary>
    /// Gets memory statistics for a session
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <returns>Memory statistics</returns>
    Task<MemoryStats> GetMemoryStatsAsync(string sessionId);

    /// <summary>
    /// Performs cleanup of old or low-importance memories
    /// </summary>
    /// <param name="sessionId">Optional session ID to clean up specific session</param>
    /// <returns>Number of memories cleaned up</returns>
    Task<int> CleanupMemoriesAsync(string? sessionId = null);

    /// <summary>
    /// Deletes all memories for a session
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <returns>Number of memories deleted</returns>
    Task<int> ClearSessionMemoriesAsync(string sessionId);

    /// <summary>
    /// Updates importance score for a memory
    /// </summary>
    /// <param name="memoryId">Memory ID</param>
    /// <param name="importance">New importance score</param>
    /// <returns>Updated memory</returns>
    Task<QdrantMemory?> UpdateMemoryImportanceAsync(string memoryId, float importance);
}

/// <summary>
/// Memory statistics for a session
/// </summary>
public class MemoryStats
{
    public int TotalCount { get; set; }
    public int TotalTokens { get; set; }
    public Dictionary<string, int> MemoryTypeDistribution { get; set; } = new();
    public float AverageImportance { get; set; }
    public DateTime? OldestMemory { get; set; }
    public DateTime? NewestMemory { get; set; }
}