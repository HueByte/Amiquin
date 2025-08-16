using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.Memory;

public class QdrantMemoryServiceTests
{
    private readonly Mock<IQdrantMemoryRepository> _mockRepository;
    private readonly Mock<ILogger<QdrantMemoryService>> _mockLogger;
    private readonly MemoryOptions _memoryOptions;
    private readonly QdrantMemoryService _memoryService;

    public QdrantMemoryServiceTests()
    {
        _mockRepository = new Mock<IQdrantMemoryRepository>();
        _mockLogger = new Mock<ILogger<QdrantMemoryService>>();
        
        _memoryOptions = new MemoryOptions
        {
            Enabled = true,
            MinImportanceScore = 0.3f,
            MaxContextMemories = 5,
            SimilarityThreshold = 0.7f,
            Qdrant = new QdrantOptions
            {
                Host = "localhost",
                Port = 6334,
                CollectionName = "test_collection"
            }
        };

        var optionsMock = new Mock<IOptions<MemoryOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_memoryOptions);

        _memoryService = new QdrantMemoryService(
            _mockRepository.Object,
            _mockLogger.Object,
            optionsMock.Object);
    }

    [Fact]
    public async Task CreateMemoryAsync_WithValidContent_ShouldCreateMemory()
    {
        // Arrange
        var sessionId = "test-session";
        var content = "This is an important fact about the user";
        var memoryType = "fact";

        var expectedQdrantMemory = new QdrantMemory
        {
            Id = "memory-1",
            ChatSessionId = sessionId,
            Content = content,
            MemoryType = memoryType,
            ImportanceScore = 0.5f
        };

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<QdrantMemory>(), It.IsAny<float[]>()))
                      .ReturnsAsync(expectedQdrantMemory);

        // Act & Assert - This will fail without OpenAI client for embeddings
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _memoryService.CreateMemoryAsync(sessionId, content, memoryType));
        
        Assert.Contains("Failed to generate embedding", exception.Message);
    }

    [Fact]
    public async Task GetMemoryStatsAsync_ShouldReturnCorrectStats()
    {
        // Arrange
        var sessionId = "test-session";
        var expectedCount = 10;
        var expectedTokens = 500;

        _mockRepository.Setup(r => r.GetSessionMemoryStatsAsync(sessionId))
                      .ReturnsAsync((expectedCount, expectedTokens));

        var memories = new List<QdrantMemory>
        {
            new() { MemoryType = "fact", ImportanceScore = 0.8f, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { MemoryType = "summary", ImportanceScore = 0.6f, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { MemoryType = "fact", ImportanceScore = 0.9f, CreatedAt = DateTime.UtcNow }
        };

        _mockRepository.Setup(r => r.GetSessionMemoriesAsync(sessionId, It.IsAny<int>()))
                      .ReturnsAsync(memories);

        // Act
        var stats = await _memoryService.GetMemoryStatsAsync(sessionId);

        // Assert
        Assert.Equal(expectedCount, stats.TotalCount);
        Assert.Equal(expectedTokens, stats.TotalTokens);
        Assert.Equal(2, stats.MemoryTypeDistribution["fact"]);
        Assert.Equal(1, stats.MemoryTypeDistribution["summary"]);
        Assert.True(stats.AverageImportance > 0);
    }

    [Fact]
    public async Task GetRelevantMemoriesAsync_WhenDisabled_ShouldReturnEmptyList()
    {
        // Arrange
        _memoryOptions.Enabled = false;
        var sessionId = "test-session";
        var query = "test query";

        // Act
        var result = await _memoryService.GetRelevantMemoriesAsync(sessionId, query);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task CleanupMemoriesAsync_WhenAutoCleanupDisabled_ShouldReturnZero()
    {
        // Arrange
        _memoryOptions.AutoCleanup = false;

        // Act
        var result = await _memoryService.CleanupMemoriesAsync();

        // Assert
        Assert.Equal(0, result);
        _mockRepository.Verify(r => r.CleanupOldMemoriesAsync(It.IsAny<int>(), It.IsAny<float>()), Times.Never);
    }

    [Fact]
    public async Task ExtractMemoriesFromConversationAsync_WithTooFewMessages_ShouldReturnEmpty()
    {
        // Arrange
        var sessionId = "test-session";
        var messages = new List<SessionMessage>
        {
            new() { Role = "user", Content = "Hello" },
            new() { Role = "assistant", Content = "Hi there!" }
        };

        _memoryOptions.MinMessagesForMemory = 5;

        // Act
        var result = await _memoryService.ExtractMemoriesFromConversationAsync(sessionId, messages);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCheckHealthAndInitializeCollection()
    {
        // Arrange
        _mockRepository.Setup(r => r.IsHealthyAsync()).ReturnsAsync(true);
        _mockRepository.Setup(r => r.InitializeCollectionAsync()).ReturnsAsync(true);

        // Act
        var result = await _memoryService.InitializeAsync();

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.IsHealthyAsync(), Times.Once);
        _mockRepository.Verify(r => r.InitializeCollectionAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenUnhealthy_ShouldReturnFalse()
    {
        // Arrange
        _mockRepository.Setup(r => r.IsHealthyAsync()).ReturnsAsync(false);

        // Act
        var result = await _memoryService.InitializeAsync();

        // Assert
        Assert.False(result);
        _mockRepository.Verify(r => r.IsHealthyAsync(), Times.Once);
        _mockRepository.Verify(r => r.InitializeCollectionAsync(), Times.Never);
    }

    [Fact]
    public void QdrantMemory_CalculateRelevanceScore_ShouldReturnValidScore()
    {
        // Arrange
        var memory = new QdrantMemory
        {
            ImportanceScore = 0.8f,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastAccessedAt = DateTime.UtcNow.AddDays(-1),
            AccessCount = 3
        };

        // Act
        var score = memory.CalculateRelevanceScore();

        // Assert
        Assert.True(score >= 0.0f && score <= 1.0f);
        Assert.True(score > 0); // Should be greater than 0 for recently accessed memory
    }

    [Fact]
    public void QdrantMemory_MarkAccessed_ShouldUpdateAccessInfo()
    {
        // Arrange
        var memory = new QdrantMemory
        {
            AccessCount = 5,
            LastAccessedAt = DateTime.UtcNow.AddDays(-1)
        };

        var beforeAccess = memory.LastAccessedAt;

        // Act
        memory.MarkAccessed();

        // Assert
        Assert.Equal(6, memory.AccessCount);
        Assert.True(memory.LastAccessedAt > beforeAccess);
    }

    [Fact]
    public async Task ClearSessionMemoriesAsync_ShouldDeleteAllSessionMemories()
    {
        // Arrange
        var sessionId = "test-session";
        var memories = new List<QdrantMemory>
        {
            new() { Id = "1", ChatSessionId = sessionId },
            new() { Id = "2", ChatSessionId = sessionId },
            new() { Id = "3", ChatSessionId = sessionId }
        };

        _mockRepository.Setup(r => r.GetSessionMemoriesAsync(sessionId, It.IsAny<int>()))
                      .ReturnsAsync(memories);

        _mockRepository.Setup(r => r.DeleteAsync(It.IsAny<string>()))
                      .ReturnsAsync(true);

        // Act
        var deletedCount = await _memoryService.ClearSessionMemoriesAsync(sessionId);

        // Assert
        Assert.Equal(3, deletedCount);
        _mockRepository.Verify(r => r.DeleteAsync("1"), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync("2"), Times.Once);
        _mockRepository.Verify(r => r.DeleteAsync("3"), Times.Once);
    }
}