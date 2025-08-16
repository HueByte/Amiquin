using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using TiktokenSharp;

namespace Amiquin.Core.Services.Memory;

/// <summary>
/// Qdrant-based service for managing conversation memories with vector embeddings
/// </summary>
public class QdrantMemoryService : IMemoryService
{
    private readonly IQdrantMemoryRepository _memoryRepository;
    private readonly ILogger<QdrantMemoryService> _logger;
    private readonly MemoryOptions _options;
    private readonly OpenAIClient? _openAIClient;
    private readonly TikToken _tokenizer;

    public QdrantMemoryService(
        IQdrantMemoryRepository memoryRepository,
        ILogger<QdrantMemoryService> logger,
        IOptions<MemoryOptions> options,
        OpenAIClient? openAIClient = null)
    {
        _memoryRepository = memoryRepository;
        _logger = logger;
        _options = options.Value;
        _openAIClient = openAIClient;
        _tokenizer = TikToken.GetEncoding("cl100k_base");
    }

    /// <inheritdoc />
    public async Task<QdrantMemory> CreateMemoryAsync(
        string sessionId, 
        string content, 
        string memoryType = "context", 
        ulong? userId = null, 
        float? importance = null)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Memory system is disabled");

        // Calculate importance score
        var importanceScore = importance ?? CalculateImportanceScore(content, memoryType);
        
        if (importanceScore < _options.MinImportanceScore)
        {
            _logger.LogDebug("Memory content has low importance score {Score}, skipping creation", importanceScore);
            throw new InvalidOperationException($"Memory importance score {importanceScore} is below minimum threshold {_options.MinImportanceScore}");
        }

        // Generate embedding
        var embedding = await GenerateEmbeddingAsync(content);
        if (embedding == null || embedding.Length == 0)
        {
            throw new InvalidOperationException("Failed to generate embedding for memory content");
        }

        // Estimate tokens
        var tokens = EstimateTokens(content);

        var qdrantMemory = new QdrantMemory
        {
            ChatSessionId = sessionId,
            Content = content,
            MemoryType = memoryType,
            UserId = userId,
            ImportanceScore = importanceScore,
            EstimatedTokens = tokens
        };

        var createdMemory = await _memoryRepository.CreateAsync(qdrantMemory, embedding);
        
        _logger.LogInformation("Created memory {MemoryId} for session {SessionId} with type {Type} and importance {Importance}", 
            createdMemory.Id, sessionId, memoryType, importanceScore);

        // Return the created memory directly
        return createdMemory;
    }

    /// <inheritdoc />
    public async Task<List<(QdrantMemory Memory, float SimilarityScore)>> GetRelevantMemoriesAsync(
        string sessionId, 
        string query, 
        int maxResults = 5)
    {
        if (!_options.Enabled)
            return new List<(QdrantMemory Memory, float SimilarityScore)>();

        var queryEmbedding = await GenerateEmbeddingAsync(query);
        if (queryEmbedding == null || queryEmbedding.Length == 0)
        {
            _logger.LogWarning("Failed to generate embedding for query, returning empty results");
            return new List<(QdrantMemory Memory, float SimilarityScore)>();
        }

        var results = await _memoryRepository.GetSimilarMemoriesAsync(
            sessionId, 
            queryEmbedding, 
            Math.Min(maxResults, _options.MaxContextMemories), 
            _options.SimilarityThreshold);

        _logger.LogInformation("Retrieved {Count} relevant memories for query in session {SessionId}", 
            results.Count, sessionId);

        // Return QdrantMemory results directly
        return results;
    }

    /// <inheritdoc />
    public async Task<List<QdrantMemory>> ExtractMemoriesFromConversationAsync(
        string sessionId, 
        List<SessionMessage> messages)
    {
        if (!_options.Enabled || messages.Count < _options.MinMessagesForMemory)
            return new List<QdrantMemory>();

        var extractedMemories = new List<QdrantMemory>();

        // Extract different types of memories
        await ExtractSummaryMemories(sessionId, messages, extractedMemories);
        await ExtractFactualMemories(sessionId, messages, extractedMemories);
        await ExtractPreferenceMemories(sessionId, messages, extractedMemories);

        _logger.LogInformation("Extracted {Count} memories from conversation in session {SessionId}", 
            extractedMemories.Count, sessionId);

        return extractedMemories;
    }

    /// <inheritdoc />
    public async Task<string?> GetMemoryContextAsync(string sessionId, string? currentQuery = null)
    {
        if (!_options.Enabled)
            return null;

        List<(QdrantMemory Memory, float SimilarityScore)> relevantMemories;

        if (!string.IsNullOrWhiteSpace(currentQuery))
        {
            // Get memories relevant to current query
            relevantMemories = await GetRelevantMemoriesAsync(sessionId, currentQuery, _options.MaxContextMemories);
        }
        else
        {
            // Get most recent/important memories
            var qdrantMemories = await _memoryRepository.GetSessionMemoriesAsync(sessionId, _options.MaxContextMemories);
            relevantMemories = qdrantMemories.Select(m => (m, 1.0f)).ToList();
        }

        if (!relevantMemories.Any())
            return null;

        var contextParts = new List<string>();
        var totalTokens = 0;

        foreach (var (memory, score) in relevantMemories)
        {
            if (totalTokens + memory.EstimatedTokens > _options.MaxMemoryTokens)
                break;

            contextParts.Add($"[{memory.MemoryType}] {memory.Content}");
            totalTokens += memory.EstimatedTokens;
        }

        if (!contextParts.Any())
            return null;

        var context = string.Join("\n", contextParts);
        _logger.LogDebug("Generated memory context with {Count} memories and {Tokens} tokens for session {SessionId}", 
            contextParts.Count, totalTokens, sessionId);

        return $"Relevant memories from previous conversations:\n{context}";
    }

    /// <inheritdoc />
    public async Task<MemoryStats> GetMemoryStatsAsync(string sessionId)
    {
        var (count, totalTokens) = await _memoryRepository.GetSessionMemoryStatsAsync(sessionId);
        var qdrantMemories = await _memoryRepository.GetSessionMemoriesAsync(sessionId);

        var stats = new MemoryStats
        {
            TotalCount = count,
            TotalTokens = totalTokens,
            AverageImportance = qdrantMemories.Any() ? qdrantMemories.Average(m => m.ImportanceScore) : 0,
            OldestMemory = qdrantMemories.OrderBy(m => m.CreatedAt).FirstOrDefault()?.CreatedAt,
            NewestMemory = qdrantMemories.OrderByDescending(m => m.CreatedAt).FirstOrDefault()?.CreatedAt,
            MemoryTypeDistribution = qdrantMemories.GroupBy(m => m.MemoryType)
                                           .ToDictionary(g => g.Key, g => g.Count())
        };

        return stats;
    }

    /// <inheritdoc />
    public async Task<int> CleanupMemoriesAsync(string? sessionId = null)
    {
        if (!_options.AutoCleanup)
            return 0;

        var cleanedUp = await _memoryRepository.CleanupOldMemoriesAsync(
            _options.MemoryRetentionDays, 
            _options.CleanupMaxImportance);

        _logger.LogInformation("Cleaned up {Count} old memories", cleanedUp);
        return cleanedUp;
    }

    /// <inheritdoc />
    public async Task<int> ClearSessionMemoriesAsync(string sessionId)
    {
        var memories = await _memoryRepository.GetSessionMemoriesAsync(sessionId);
        var deletedCount = 0;

        foreach (var memory in memories)
        {
            if (await _memoryRepository.DeleteAsync(memory.Id))
                deletedCount++;
        }

        _logger.LogInformation("Cleared {Count} memories for session {SessionId}", deletedCount, sessionId);
        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<QdrantMemory?> UpdateMemoryImportanceAsync(string memoryId, float importance)
    {
        // Since we need to search by ID, we'll get all memories and find the one
        // This is not ideal for large datasets, but works for the current implementation
        var allMemories = await _memoryRepository.GetSessionMemoriesAsync("", int.MaxValue);
        var qdrantMemory = allMemories.FirstOrDefault(m => m.Id == memoryId);
        
        if (qdrantMemory == null)
            return null;

        qdrantMemory.ImportanceScore = Math.Clamp(importance, 0.0f, 1.0f);
        var updatedMemory = await _memoryRepository.UpdateAsync(qdrantMemory);
        
        return updatedMemory;
    }

    /// <summary>
    /// Initializes the Qdrant collection and checks health
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        var isHealthy = await _memoryRepository.IsHealthyAsync();
        if (!isHealthy)
        {
            _logger.LogWarning("Qdrant health check failed");
            return false;
        }

        var initialized = await _memoryRepository.InitializeCollectionAsync();
        if (initialized)
        {
            _logger.LogInformation("Qdrant memory service initialized successfully");
        }

        return initialized;
    }

    private async Task<float[]?> GenerateEmbeddingAsync(string text)
    {
        if (_openAIClient == null)
        {
            _logger.LogWarning("OpenAI client not available, cannot generate embeddings");
            return null;
        }

        try
        {
            var embeddingClient = _openAIClient.GetEmbeddingClient(_options.EmbeddingModel);
            var response = await embeddingClient.GenerateEmbeddingAsync(text);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text");
            return null;
        }
    }

    private float CalculateImportanceScore(string content, string memoryType)
    {
        var baseScore = 0.5f;

        // Apply memory type multiplier
        if (_options.MemoryTypeImportance.TryGetValue(memoryType, out var typeMultiplier))
        {
            baseScore *= typeMultiplier;
        }

        // Adjust based on content length and characteristics
        var tokens = EstimateTokens(content);
        if (tokens > 100) baseScore += 0.1f; // Longer content might be more important
        if (content.Contains("important") || content.Contains("remember")) baseScore += 0.2f;
        if (content.Contains("?")) baseScore += 0.1f; // Questions might be important

        return Math.Clamp(baseScore, 0.0f, 1.0f);
    }

    private int EstimateTokens(string text)
    {
        try
        {
            return _tokenizer.Encode(text).Count;
        }
        catch
        {
            // Fallback estimation: roughly 4 characters per token
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }


    private async Task ExtractSummaryMemories(string sessionId, List<SessionMessage> messages, List<QdrantMemory> memories)
    {
        // Simple implementation - could be enhanced with AI summarization
        var conversationText = string.Join(" ", messages.Where(m => m.Role == "user" || m.Role == "assistant")
                                                        .Select(m => m.Content));
        
        if (conversationText.Length > 500)
        {
            var summary = conversationText.Length > 1000 
                ? conversationText.Substring(0, 1000) + "..." 
                : conversationText;
            
            try
            {
                var memory = await CreateMemoryAsync(sessionId, summary, "summary");
                memories.Add(memory);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to create summary memory: {Error}", ex.Message);
            }
        }
    }

    private async Task ExtractFactualMemories(string sessionId, List<SessionMessage> messages, List<QdrantMemory> memories)
    {
        // Extract potential facts from user messages
        var userMessages = messages.Where(m => m.Role == "user").ToList();
        
        foreach (var message in userMessages)
        {
            // Simple heuristics for factual content
            if (message.Content.Contains("I am") || 
                message.Content.Contains("I like") || 
                message.Content.Contains("My name is"))
            {
                try
                {
                    var memory = await CreateMemoryAsync(sessionId, message.Content, "fact");
                    memories.Add(memory);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to create fact memory: {Error}", ex.Message);
                }
            }
        }
    }

    private async Task ExtractPreferenceMemories(string sessionId, List<SessionMessage> messages, List<QdrantMemory> memories)
    {
        // Extract preferences from user messages
        var userMessages = messages.Where(m => m.Role == "user").ToList();
        
        foreach (var message in userMessages)
        {
            if (message.Content.Contains("prefer") || 
                message.Content.Contains("don't like") || 
                message.Content.Contains("favorite"))
            {
                try
                {
                    var memory = await CreateMemoryAsync(sessionId, message.Content, "preference");
                    memories.Add(memory);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to create preference memory: {Error}", ex.Message);
                }
            }
        }
    }
}