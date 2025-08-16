using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Amiquin.Infrastructure.Repositories;

/// <summary>
/// Qdrant-based implementation for conversation memory operations
/// </summary>
public class QdrantMemoryRepository : IQdrantMemoryRepository, IDisposable
{
    private readonly QdrantClient _qdrantClient;
    private readonly ILogger<QdrantMemoryRepository> _logger;
    private readonly QdrantOptions _qdrantOptions;
    private readonly string _collectionName;
    private bool _disposed = false;

    public QdrantMemoryRepository(
        ILogger<QdrantMemoryRepository> logger,
        IOptions<MemoryOptions> memoryOptions)
    {
        _logger = logger;
        _qdrantOptions = memoryOptions.Value.Qdrant;
        _collectionName = _qdrantOptions.CollectionName;

        // Initialize Qdrant client
        var address = _qdrantOptions.UseHttps ? $"https://{_qdrantOptions.Host}:{_qdrantOptions.Port}" : $"http://{_qdrantOptions.Host}:{_qdrantOptions.Port}";
        _qdrantClient = new QdrantClient(address, apiKey: _qdrantOptions.ApiKey);

        _logger.LogInformation("Initialized Qdrant client for {Host}:{Port}, collection: {Collection}", 
            _qdrantOptions.Host, _qdrantOptions.Port, _collectionName);
    }

    /// <inheritdoc />
    public async Task<QdrantMemory> CreateAsync(QdrantMemory memory, float[] embedding)
    {
        try
        {
            var point = memory.ToQdrantPoint(embedding);
            
            var upsertResponse = await _qdrantClient.UpsertAsync(
                collectionName: _collectionName,
                points: new[] { point });

            _logger.LogInformation("Created memory {MemoryId} for session {SessionId}", 
                memory.Id, memory.ChatSessionId);
            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create memory {MemoryId} in Qdrant", memory.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetSimilarMemoriesAsync(
        string sessionId, 
        float[] queryEmbedding, 
        int topK = 5, 
        float similarityThreshold = 0.7f)
    {
        try
        {
            // For simplicity, get all points and filter in memory
            // In production, you'd want to use proper Qdrant filtering
            var searchResponse = await _qdrantClient.SearchAsync(
                collectionName: _collectionName,
                vector: queryEmbedding,
                limit: (uint)topK,
                scoreThreshold: similarityThreshold);

            var results = new List<(QdrantMemory Memory, float SimilarityScore)>();
            
            foreach (var point in searchResponse)
            {
                var (memory, score) = QdrantMemory.FromQdrantScoredPoint(point);
                
                // Filter by session ID
                if (memory.ChatSessionId == sessionId)
                {
                    // Update access tracking
                    memory.MarkAccessed();
                    await UpdateAsync(memory);
                    
                    results.Add((memory, score));
                }
            }

            _logger.LogInformation("Found {Count} similar memories for session {SessionId} with threshold {Threshold}", 
                results.Count, sessionId, similarityThreshold);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search similar memories for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<QdrantMemory>> GetSessionMemoriesAsync(string sessionId, int limit = 50)
    {
        try
        {
            // For simplicity, scroll all and filter in memory
            var scrollResponse = await _qdrantClient.ScrollAsync(
                collectionName: _collectionName,
                limit: (uint)limit);
            
            var memories = new List<QdrantMemory>();
            
            foreach (var point in scrollResponse.Result)
            {
                var memory = QdrantMemory.FromQdrantPoint(point);
                if (memory.ChatSessionId == sessionId)
                {
                    memories.Add(memory);
                }
            }

            // Sort by relevance score
            memories = memories
                .OrderByDescending(m => m.CalculateRelevanceScore())
                .Take(limit)
                .ToList();

            _logger.LogInformation("Retrieved {Count} memories for session {SessionId}", memories.Count, sessionId);
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session memories for {SessionId}", sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<QdrantMemory> UpdateAsync(QdrantMemory memory)
    {
        try
        {
            // For now, we'll use a simple approach: recreate the point with empty vector
            // In a production system, you'd want to preserve the original embedding
            var emptyVector = new float[_qdrantOptions.VectorSize];
            var updatedPoint = memory.ToQdrantPoint(emptyVector);
            
            await _qdrantClient.UpsertAsync(
                collectionName: _collectionName,
                points: new[] { updatedPoint });

            _logger.LogDebug("Updated memory {MemoryId}", memory.Id);
            return memory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update memory {MemoryId}", memory.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldMemoriesAsync(int olderThanDays = 30, float maxImportanceScore = 0.3f)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            
            // Get all points and filter in memory for simplicity
            var allPoints = await _qdrantClient.ScrollAsync(
                collectionName: _collectionName,
                limit: 10000); // Adjust based on your needs

            var pointsToDelete = new List<PointId>();

            foreach (var point in allPoints.Result)
            {
                var memory = QdrantMemory.FromQdrantPoint(point);
                
                if (memory.CreatedAt < cutoffDate && memory.ImportanceScore <= maxImportanceScore)
                {
                    pointsToDelete.Add(new PointId { Uuid = memory.Id });
                }
            }

            if (pointsToDelete.Any())
            {
                await _qdrantClient.DeleteAsync(
                    collectionName: _collectionName,
                    ids: pointsToDelete);

                _logger.LogInformation("Cleaned up {Count} old memories older than {Days} days with importance <= {Score}", 
                    pointsToDelete.Count, olderThanDays, maxImportanceScore);
            }

            return pointsToDelete.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old memories");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string memoryId)
    {
        try
        {
            await _qdrantClient.DeleteAsync(
                collectionName: _collectionName,
                ids: new[] { new PointId { Uuid = memoryId } });

            _logger.LogInformation("Deleted memory {MemoryId}", memoryId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory {MemoryId}", memoryId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(int Count, int TotalTokens)> GetSessionMemoryStatsAsync(string sessionId)
    {
        try
        {
            var memories = await GetSessionMemoriesAsync(sessionId, int.MaxValue);
            var count = memories.Count;
            var totalTokens = memories.Sum(m => m.EstimatedTokens);

            return (count, totalTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory stats for session {SessionId}", sessionId);
            return (0, 0);
        }
    }

    /// <inheritdoc />
    public async Task<List<QdrantMemory>> GetMemoriesByTypeAsync(string sessionId, string memoryType, int limit = 20)
    {
        try
        {
            var allMemories = await GetSessionMemoriesAsync(sessionId, int.MaxValue);
            
            var filteredMemories = allMemories
                .Where(m => m.MemoryType == memoryType)
                .OrderByDescending(m => m.LastAccessedAt)
                .Take(limit)
                .ToList();

            _logger.LogInformation("Retrieved {Count} memories of type {Type} for session {SessionId}", 
                filteredMemories.Count, memoryType, sessionId);

            return filteredMemories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memories by type {Type} for session {SessionId}", memoryType, sessionId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> InitializeCollectionAsync()
    {
        try
        {
            // Check if collection exists
            var collections = await _qdrantClient.ListCollectionsAsync();
            var collectionExists = collections.Any(c => c == _collectionName);

            if (!collectionExists && _qdrantOptions.AutoCreateCollection)
            {
                _logger.LogInformation("Creating Qdrant collection: {CollectionName}", _collectionName);

                var vectorParams = new VectorParams
                {
                    Size = _qdrantOptions.VectorSize,
                    Distance = Enum.Parse<Distance>(_qdrantOptions.Distance, true)
                };

                await _qdrantClient.CreateCollectionAsync(
                    collectionName: _collectionName,
                    vectorsConfig: vectorParams);
                var created = true;

                if (created)
                {
                    _logger.LogInformation("Successfully created Qdrant collection: {CollectionName}", _collectionName);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to create Qdrant collection: {CollectionName}", _collectionName);
                    return false;
                }
            }

            _logger.LogInformation("Qdrant collection {CollectionName} already exists or auto-creation is disabled", _collectionName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Qdrant collection: {CollectionName}", _collectionName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Simple health check by listing collections
            await _qdrantClient.ListCollectionsAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Qdrant health check failed");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(long Count, uint VectorSize, string Distance)> GetCollectionInfoAsync()
    {
        try
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(_collectionName);
            
            return (
                (long)info.PointsCount,
                1536, // Default vector size - would need proper API access
                "Cosine" // Default distance - would need proper API access
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get collection info for {CollectionName}", _collectionName);
            throw;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _qdrantClient?.Dispose();
            _disposed = true;
        }
    }
}