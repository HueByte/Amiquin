using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
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
    private readonly Mock<BotContextAccessor> _mockBotContextAccessor;
    private readonly Mock<IOptions<BotOptions>> _mockBotOptions;
    private readonly Mock<SemaphoreSlim> _mockSemaphore;
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
        _mockBotContextAccessor = new Mock<BotContextAccessor>();
        _mockBotOptions = new Mock<IOptions<BotOptions>>();
        _mockSemaphore = new Mock<SemaphoreSlim>(1, 1);

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        
        _botOptions = new BotOptions
        {
            Name = "TestAmiquin"
            // Version is read-only property from assembly
        };
        
        _mockBotOptions.Setup(o => o.Value).Returns(_botOptions);
        _mockChatSemaphoreManager
            .Setup(m => m.GetOrCreateInstanceSemaphore(It.IsAny<ulong>()))
            .Returns(_mockSemaphore.Object);

        _personaService = new PersonaService(
            _mockLogger.Object,
            _mockMessageCacheService.Object,
            _mockCoreChatService.Object,
            _mockNewsApiClient.Object,
            _memoryCache,
            _mockChatSemaphoreManager.Object,
            _mockServerMetaService.Object,
            _mockBotContextAccessor.Object,
            _mockBotOptions.Object
        );
    }

    [Fact]
    public async Task GetPersonaAsync_WithBasePersonaFile_LoadsAndCombinesWithServerPersona()
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
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync("Mood-based persona addition");

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("TestAmiquin", result); // Should contain bot name replacement
        Assert.Contains("1.0.0-test", result); // Should contain version replacement
        
        // Should contain server-specific section
        Assert.Contains("## Server-Specific Instructions", result);
        Assert.Contains(serverPersona, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithoutServerPersona_UsesBasePersonaWithDefaults()
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
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync("Mood-based persona addition");

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("TestAmiquin", result);
        Assert.Contains(Constants.PersonaDefaults.DefaultPersonaTemplate, result);
        Assert.DoesNotContain("## Server-Specific Instructions", result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithCachedPersona_ReturnsCachedResult()
    {
        // Arrange
        var serverId = 12345UL;
        var cachedPersona = "Cached persona content";
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
        
        _memoryCache.Set(cacheKey, cachedPersona);

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.Equal(cachedPersona, result);
        
        // Should not call server meta service if cached
        _mockServerMetaService.Verify(s => s.GetServerMetaAsync(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBasePersonaCache_UsesCache()
    {
        // Arrange
        var serverId = 12345UL;
        var cachedBasePersona = "# Cached Base Persona\nThis is cached content";
        
        // Set base persona in cache
        _memoryCache.Set("BasePersona", cachedBasePersona);
        
        var serverMeta = new Core.Models.ServerMeta 
        { 
            Id = serverId, 
            Persona = "Server specific content" 
        };

        _mockServerMetaService
            .Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync("Mood content");

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(cachedBasePersona, result);
        Assert.Contains("## Server-Specific Instructions", result);
        Assert.Contains("Server specific content", result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBotContextAccessor_UsesContextBotName()
    {
        // Arrange
        var serverId = 12345UL;
        var contextBotName = "DynamicBotName";
        
        _mockBotContextAccessor.Setup(b => b.IsInitialized).Returns(true);
        _mockBotContextAccessor.Setup(b => b.BotName).Returns(contextBotName);

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync($"{contextBotName} just received some news...");

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.Contains(contextBotName, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithBotContextAccessorFailure_FallsBackToBotOptions()
    {
        // Arrange
        var serverId = 12345UL;
        
        _mockBotContextAccessor.Setup(b => b.IsInitialized).Returns(false);
        // or could setup to throw exception to simulate failure

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync($"{_botOptions.Name} fallback news content...");

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
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
        
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
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync("Generated mood content");

        // Act
        await _personaService.AddSummaryAsync(serverId, summaryMessage);

        // Assert
        var cacheKey = StringModifier.CreateCacheKey(Constants.CacheKeys.ComputedPersonaMessageKey, serverId.ToString());
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
        var semaphoreWaitCalled = false;
        var semaphoreReleaseCalled = false;

        _mockSemaphore.Setup(s => s.WaitAsync()).Callback(() => semaphoreWaitCalled = true).Returns(Task.CompletedTask);
        _mockSemaphore.Setup(s => s.Release()).Callback(() => semaphoreReleaseCalled = true);

        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        _mockCoreChatService
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync("Test mood content");

        // Act
        await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.True(semaphoreWaitCalled);
        Assert.True(semaphoreReleaseCalled);
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
    public async Task GetPersonaAsync_ReplacesPersonaKeywords_Correctly()
    {
        // Arrange
        var serverId = 12345UL;
        var basePersona = $"Bot name: {Constants.PersonaKeywordsCache.Name}, Version: {Constants.PersonaKeywordsCache.Version}, Mood: {Constants.PersonaKeywordsCache.Mood}";
        
        _memoryCache.Set("BasePersona", basePersona);
        
        var serverMeta = new Core.Models.ServerMeta { Id = serverId, Persona = null };
        _mockServerMetaService.Setup(s => s.GetServerMetaAsync(serverId)).ReturnsAsync(serverMeta);

        var moodContent = "Happy and energetic today";
        _mockCoreChatService
            .Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), It.IsAny<OpenAI.Chat.ChatMessage>(), It.IsAny<int>(), It.IsAny<ulong?>()))
            .ReturnsAsync(moodContent);

        // Act
        var result = await _personaService.GetPersonaAsync(serverId);

        // Assert
        Assert.Contains(_botOptions.Name, result);
        Assert.Contains(_botOptions.Version, result);
        Assert.Contains(moodContent, result);
        
        // Should not contain the placeholder keywords
        Assert.DoesNotContain(Constants.PersonaKeywordsCache.Name, result);
        Assert.DoesNotContain(Constants.PersonaKeywordsCache.Version, result);
        Assert.DoesNotContain(Constants.PersonaKeywordsCache.Mood, result);
    }

    public void Dispose()
    {
        _memoryCache?.Dispose();
        _mockSemaphore?.Object?.Dispose();
    }
}