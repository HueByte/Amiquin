using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for Qdrant-based conversation memory operations
/// </summary>
public interface IQdrantMemoryRepository
{
    /// <summary>
    /// Stores a new conversation memory in Qdrant
    /// </summary>
    /// <param name="memory">The memory to store</param>
    /// <param name="embedding">Vector embedding for the memory</param>
    /// <returns>The stored memory</returns>
    Task<QdrantMemory> CreateAsync(QdrantMemory memory, float[] embedding);

    /// <summary>
    /// Retrieves memories similar to the given embedding vector
    /// </summary>
    /// <param name="sessionId">Chat session ID to search within</param>
    /// <param name="queryEmbedding">Query embedding vector</param>
    /// <param name="topK">Number of top similar memories to return</param>
    /// <param name="similarityThreshold">Minimum similarity score (0.0 to 1.0)</param>
    /// <returns>List of similar memories ordered by similarity score</returns>
    Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetSimilarMemoriesAsync(
        string sessionId, 
        float[] queryEmbedding, 
        int topK = 5, 
        float similarityThreshold = 0.7f);

    /// <summary>
    /// Retrieves all memories for a specific session
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="limit">Maximum number of memories to return</param>
    /// <returns>List of memories</returns>
    Task<List<QdrantMemory>> GetSessionMemoriesAsync(string sessionId, int limit = 50);

    /// <summary>
    /// Updates an existing memory (typically for access tracking)
    /// </summary>
    /// <param name="memory">The memory to update</param>
    /// <returns>The updated memory</returns>
    Task<QdrantMemory> UpdateAsync(QdrantMemory memory);

    /// <summary>
    /// Deletes memories older than specified days with low importance scores
    /// </summary>
    /// <param name="olderThanDays">Delete memories older than this many days</param>
    /// <param name="maxImportanceScore">Only delete memories with importance score below this threshold</param>
    /// <returns>Number of memories deleted</returns>
    Task<int> CleanupOldMemoriesAsync(int olderThanDays = 30, float maxImportanceScore = 0.3f);

    /// <summary>
    /// Deletes a specific memory by ID
    /// </summary>
    /// <param name="memoryId">Memory ID to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(string memoryId);

    /// <summary>
    /// Gets memory statistics for a session
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <returns>Memory count and total estimated tokens</returns>
    Task<(int Count, int TotalTokens)> GetSessionMemoryStatsAsync(string sessionId);

    /// <summary>
    /// Retrieves memories by type for a specific session
    /// </summary>
    /// <param name="sessionId">Chat session ID</param>
    /// <param name="memoryType">Type of memory to retrieve</param>
    /// <param name="limit">Maximum number of memories to return</param>
    /// <returns>List of memories of the specified type</returns>
    Task<List<QdrantMemory>> GetMemoriesByTypeAsync(string sessionId, string memoryType, int limit = 20);

    /// <summary>
    /// Initializes the Qdrant collection (creates if doesn't exist)
    /// </summary>
    /// <returns>True if collection was created or already exists</returns>
    Task<bool> InitializeCollectionAsync();

    /// <summary>
    /// Gets the health status of the Qdrant connection
    /// </summary>
    /// <returns>True if Qdrant is healthy and accessible</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Gets collection information
    /// </summary>
    /// <returns>Collection info including count and configuration</returns>
    Task<(long Count, uint VectorSize, string Distance)> GetCollectionInfoAsync();

    // Cross-session memory retrieval methods for long-term memory

    /// <summary>
    /// Retrieves memories for a specific user across all sessions
    /// </summary>
    /// <param name="userId">Discord User ID</param>
    /// <param name="queryEmbedding">Optional query embedding for similarity search</param>
    /// <param name="topK">Maximum number of memories to return</param>
    /// <param name="similarityThreshold">Minimum similarity score (0.0 to 1.0)</param>
    /// <returns>List of user memories with similarity scores</returns>
    Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetUserMemoriesAsync(
        ulong userId,
        float[]? queryEmbedding = null,
        int topK = 10,
        float similarityThreshold = 0.6f);

    /// <summary>
    /// Retrieves memories for a specific server across all users and sessions
    /// </summary>
    /// <param name="serverId">Discord Server/Guild ID</param>
    /// <param name="queryEmbedding">Optional query embedding for similarity search</param>
    /// <param name="topK">Maximum number of memories to return</param>
    /// <param name="similarityThreshold">Minimum similarity score (0.0 to 1.0)</param>
    /// <returns>List of server memories with similarity scores</returns>
    Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetServerMemoriesAsync(
        ulong serverId,
        float[]? queryEmbedding = null,
        int topK = 10,
        float similarityThreshold = 0.6f);

    /// <summary>
    /// Retrieves combined memories from session, user, and server scopes
    /// </summary>
    /// <param name="sessionId">Current chat session ID</param>
    /// <param name="userId">Discord User ID</param>
    /// <param name="serverId">Discord Server/Guild ID</param>
    /// <param name="queryEmbedding">Query embedding for similarity search</param>
    /// <param name="maxSessionMemories">Maximum session-scoped memories</param>
    /// <param name="maxUserMemories">Maximum user-scoped memories</param>
    /// <param name="maxServerMemories">Maximum server-scoped memories</param>
    /// <param name="similarityThreshold">Minimum similarity score</param>
    /// <returns>Combined list of memories from all scopes</returns>
    Task<List<(QdrantMemory Memory, float SimilarityScore, MemoryScope Source)>> GetCombinedMemoriesAsync(
        string sessionId,
        ulong userId,
        ulong serverId,
        float[] queryEmbedding,
        int maxSessionMemories = 5,
        int maxUserMemories = 3,
        int maxServerMemories = 2,
        float similarityThreshold = 0.65f);

    /// <summary>
    /// Gets memory statistics for a specific user across all sessions
    /// </summary>
    /// <param name="userId">Discord User ID</param>
    /// <returns>Memory count and total estimated tokens</returns>
    Task<(int Count, int TotalTokens)> GetUserMemoryStatsAsync(ulong userId);

    /// <summary>
    /// Gets memory statistics for a specific server
    /// </summary>
    /// <param name="serverId">Discord Server/Guild ID</param>
    /// <returns>Memory count and total estimated tokens</returns>
    Task<(int Count, int TotalTokens)> GetServerMemoryStatsAsync(ulong serverId);

    /// <summary>
    /// Deletes all memories for a specific user
    /// </summary>
    /// <param name="userId">Discord User ID</param>
    /// <returns>Number of memories deleted</returns>
    Task<int> DeleteUserMemoriesAsync(ulong userId);
}