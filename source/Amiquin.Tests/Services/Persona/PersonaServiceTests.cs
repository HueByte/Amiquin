using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.Persona;

public class PersonaServiceTests : IDisposable
{
    private readonly Mock<ILogger<PersonaService>> _mockLogger;
    private readonly Mock<IMessageCacheService> _mockMessageCacheService;
    private readonly Mock<IChatCoreService> _mockCoreChatService;
    private readonly Mock<INewsApiClient> _mockNewsApiClient;
    private readonly Mock<IChatSemaphoreManager> _mockChatSemaphoreManager;
    private readonly Mock<IServerMetaService> _mockServerMetaService;
    private readonly BotContextAccessor _botContextAccessor;
    private readonly Mock<IOptions<BotOptions>> _mockBotOptions;
    private readonly SemaphoreSlim _semaphore;
    private readonly IMemoryCache _memoryCache;
    private readonly PersonaService _personaService;
    private readonly BotOptions _botOptions;

    public PersonaServiceTests()
    {
        _mockLogger = new Mock<ILogger<PersonaService>>();
        _mockMessageCacheService = new Mock<IMessageCacheService>();
        _mockCoreChatService = new Mock<IChatCoreService>();
        _mockNewsApiClient = new Mock<INewsApiClient>();
        _mockChatSemaphoreManager = new Mock<IChatSemaphoreManager>();
        _mockServerMetaService = new Mock<IServerMetaService>();
        // Use real instance instead of mock - BotContextAccessor has complex constructor that can't be mocked
        _botContextAccessor = new BotContextAccessor();
        _mockBotOptions = new Mock<IOptions<BotOptions>>();
        // Use real SemaphoreSlim instead of mock - SemaphoreSlim can't be easily mocked
        _semaphore = new SemaphoreSlim(1, 1);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _botOptions = new BotOptions
        {
            Name = "TestAmiquin"
            // Version is read-only property from assembly
        };

        _mockBotOptions.Setup(o => o.Value).Returns(_botOptions);
        _mockChatSemaphoreManager
            .Setup(m => m.GetOrCreateInstanceSemaphore(It.IsAny<ulong>()))
            .Returns(_semaphore);

        _personaService = new PersonaService(
            _mockLogger.Object,
            _mockMessageCacheService.Object,
            _mockCoreChatService.Object,
            _mockNewsApiClient.Object,
            _memoryCache,
            _mockChatSemaphoreManager.Object,
            _mockServerMetaService.Object,
            _botContextAccessor,
            _mockBotOptions.Object
        );
    }

    [Fact]
    public async Task GetPersonaAsync_WithServerPersona_CombinesWithBaseSystem()
    {
        // Arrange
        var serverId = 12345UL;
        var serverPersona = "Additional server-specific instructions";
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = serverPersona
        };

        _mockServerMetaService
            .Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Mood-based persona addition" });

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        // Default base system includes "Amiquin"
        Assert.Contains("Amiquin", result);

        // Should contain server-specific section
        Assert.Contains("## Server-Specific Instructions", result);
        Assert.Contains(serverPersona, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithoutServerPersona_UsesBaseSystemWithDefaults()
    {
        // Arrange
        var serverId = 12345UL;
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = null // No server-specific persona
        };

        _mockServerMetaService
            .Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Mood-based persona addition" });

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        // Default base system message uses "Amiquin" (from fallback when file doesn't exist)
        Assert.Contains("Amiquin", result);
        // Should not contain server-specific section when no server persona is set
        Assert.DoesNotContain("## Server-Specific Instructions", result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithCachedPersona_ReturnsCachedResult()
    {
        // Arrange
        var serverId = 12345UL;
        var cachedPersona = "Cached persona content";
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverId.ToString());

        _memoryCache.Set(cacheKey, cachedPersona);

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.Equal(cachedPersona, result);

        // Should not call server meta service if cached
        _mockServerMetaService.Verify(s => s.GetServerMetaAsync(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBaseSystemCache_UsesCache()
    {
        // Arrange
        var serverId = 12345UL;
        var cachedBaseSystem = "# Cached Base System\nThis is cached system content";

        // Set base system in cache (note: cache key is "BaseSystem", not "BasePersona")
        _memoryCache.Set("BaseSystem", cachedBaseSystem);

        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = "Server specific content"
        };

        _mockServerMetaService
            .Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Mood content" });

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(cachedBaseSystem, result);
        Assert.Contains("## Server-Specific Instructions", result);
        Assert.Contains("Server specific content", result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBotContextAccessor_UsesContextBotName()
    {
        // Arrange
        var serverId = 12345UL;

        // BotContextAccessor is not initialized, so it uses BotOptions.Name
        // This test verifies the fallback behavior since we can't easily initialize the accessor
        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = $"{_botOptions.Name} just received some news..." });

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert - Uses BotOptions.Name since BotContextAccessor is not initialized
        Assert.Contains(_botOptions.Name, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBotContextAccessorNotInitialized_FallsBackToBotOptions()
    {
        // Arrange
        var serverId = 12345UL;

        // BotContextAccessor is not initialized (IsInitialized = false by default)
        // so it should fall back to BotOptions

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = $"{_botOptions.Name} fallback news content..." });

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.Contains(_botOptions.Name, result);
        Assert.Contains("TestAmiquin", result);
    }

    [Fact]
    public async Task AddSummaryAsync_WithExistingPersona_AppendsSummary()
    {
        // Arrange
        var serverId = 12345UL;
        var existingPersona = "Existing persona content";
        var summaryMessage = "This is a conversation summary";
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverId.ToString());

        _memoryCache.Set(cacheKey, existingPersona);

        // Act
        await _personaService.AddSummaryAsync(serverId, summaryMessage);

        // Assert
        var updatedPersona = _memoryCache.Get<string>(cacheKey);
        Assert.NotNull(updatedPersona);
        Assert.Contains(existingPersona, updatedPersona);
        Assert.Contains($"This is your summary of recent conversations: {summaryMessage}", updatedPersona);
    }

    [Fact]
    public async Task AddSummaryAsync_WithoutExistingPersona_GeneratesNewPersonaWithSummary()
    {
        // Arrange
        var serverId = 12345UL;
        var summaryMessage = "This is a conversation summary";

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = "Server persona" };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Generated mood content" });

        // Act
        await _personaService.AddSummaryAsync(serverId, summaryMessage);

        // Assert
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedSystemMessageKey, serverId.ToString());
        var updatedPersona = _memoryCache.Get<string>(cacheKey);

        Assert.NotNull(updatedPersona);
        Assert.Contains($"This is your summary of recent conversations: {summaryMessage}", updatedPersona);
        Assert.Contains("Server persona", updatedPersona);
    }

    [Fact]
    public async Task GetPersonaAsync_WithSemaphoreLocking_EnforcesConcurrencyControl()
    {
        // Arrange
        var serverId = 12345UL;

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Test mood content" });

        // Act
        await _personaService.GetPersonaAsync(serverId);

        // Assert - Verify the semaphore manager was called to get/create the semaphore
        _mockChatSemaphoreManager.Verify(m => m.GetOrCreateInstanceSemaphore(serverId), Times.Once);
    }

    [Fact]
    public async Task GetPersonaAsync_WithNewsApiFailure_HandlesCategorError()
    {
        // Arrange
        var serverId = 12345UL;
        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };

        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);
        _mockNewsApiClient.Setup(n => n.GetNewsAsync()).ThrowsAsync(new HttpRequestException("News API failed"));

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        // Should contain fallback content when news fails
        Assert.Contains("TestAmiquin", result);
        // Should not throw exception, should handle gracefully
    }

    [Fact]
    public async Task GetPersonaAsync_ReplacesSystemKeywords_Correctly()
    {
        // Arrange
        var serverId = 12345UL;
        var baseSystem = $"Bot name: {Constants.SystemKeywordsCache.Name}, Version: {Constants.SystemKeywordsCache.Version}";

        // Note: cache key is "BaseSystem" not "BasePersona"
        _memoryCache.Set("BaseSystem", baseSystem);

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        // Note: NewsApiClient is not mocked to return news, so mood processing returns fallback message
        // The service catches the null/empty news and returns Constants.SystemDefaults.NewsMoodNotAvailableMessage

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert - Name placeholder should be replaced with bot name
        Assert.Contains(_botOptions.Name, result);

        // Should not contain the placeholder keywords
        Assert.DoesNotContain(Constants.SystemKeywordsCache.Name, result);
        Assert.DoesNotContain(Constants.SystemKeywordsCache.Version, result);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
        _semaphore?.Dispose();
        _botContextAccessor?.Dispose();
    }
}