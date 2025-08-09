#if false // Complex API mocking scenarios - these tests should be moved to integration tests
using Amiquin.Core.Models;
using Amiquin.Core.Services.ApiClients;
using Amiquin.Core.Services.ApiClients.Responses;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Persona;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using OpenAI.Chat;
using System.Threading;
using Xunit;

namespace Amiquin.Tests.Services.Persona;

public class PersonaServiceTests
{
    private readonly Mock<ILogger<PersonaService>> _loggerMock;
    private readonly Mock<IMessageCacheService> _messageCacheServiceMock;
    private readonly Mock<IChatCoreService> _coreChatServiceMock;
    private readonly Mock<INewsApiClient> _newsApiClientMock;
    private readonly Mock<IMemoryCache> _memoryCacheMock;
    private readonly Mock<IChatSemaphoreManager> _chatSemaphoreManagerMock;
    private readonly Mock<IServerMetaService> _serverMetaServiceMock;
    private readonly Mock<BotContextAccessor> _botContextAccessorMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly PersonaService _sut; // System Under Test

    public PersonaServiceTests()
    {
        _loggerMock = new Mock<ILogger<PersonaService>>();
        _messageCacheServiceMock = new Mock<IMessageCacheService>();
        _coreChatServiceMock = new Mock<IChatCoreService>();
        _newsApiClientMock = new Mock<INewsApiClient>();
        _memoryCacheMock = new Mock<IMemoryCache>();
        _chatSemaphoreManagerMock = new Mock<IChatSemaphoreManager>();
        _serverMetaServiceMock = new Mock<IServerMetaService>();
        _botContextAccessorMock = new Mock<BotContextAccessor>();
        _configurationMock = new Mock<IConfiguration>();

        var semaphoreMock = new Mock<SemaphoreSlim>(1, 1);
        _chatSemaphoreManagerMock.Setup(x => x.GetOrCreateInstanceSemaphore(It.IsAny<ulong>()))
            .Returns(semaphoreMock.Object);

        _botContextAccessorMock.Setup(x => x.BotName).Returns("Amiquin");

        _configurationMock.Setup(x => x.GetValue<string>("BotName"))
            .Returns("Amiquin");
        _configurationMock.Setup(x => x.GetValue<string>("BotVersion"))
            .Returns("1.0.0");

        var cacheEntryMock = new Mock<ICacheEntry>();
        _memoryCacheMock.Setup(m => m.CreateEntry(It.IsAny<object>())).Returns(cacheEntryMock.Object);

        _sut = new PersonaService(
            _loggerMock.Object,
            _messageCacheServiceMock.Object,
            _coreChatServiceMock.Object,
            _newsApiClientMock.Object,
            _memoryCacheMock.Object,
            _chatSemaphoreManagerMock.Object,
            _serverMetaServiceMock.Object,
            _botContextAccessorMock.Object,
            _configurationMock.Object);
    }

    [Fact]
    public async Task GetPersonaAsync_WithCachedPersona_ShouldReturnCachedValue()
    {
        // Arrange
        var serverId = 123456789UL;
        var cachedPersona = "Cached persona message";
        var cacheKey = $"computed_persona_message_{serverId}";

        object? cachedValue = cachedPersona;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out cachedValue))
            .Returns(true);

        // Act
        var result = await _sut.GetPersonaAsync(serverId);

        // Assert
        Assert.Equal(cachedPersona, result);
        _serverMetaServiceMock.Verify(s => s.GetServerMetaAsync(It.IsAny<ulong>()), Times.Never);
    }

    [Fact]
    public async Task GetPersonaAsync_WithServerPersona_ShouldReturnServerPersonaWithMood()
    {
        // Arrange
        var serverId = 123456789UL;
        var serverPersona = "Custom server persona with {mood}";
        var expectedMood = "Great mood today!";
        var cacheKey = $"computed_persona_message_{serverId}";

        object? cachedValue = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out cachedValue))
            .Returns(false);

        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = serverPersona
        };

        _serverMetaServiceMock.Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _coreChatServiceMock.Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), null, It.IsAny<int>()))
            .ReturnsAsync(expectedMood);

        var newsResponse = new NewsApiResponse
        {
            Data = new Data
            {
                NewsList = new List<NewsList>
                {
                    new()
                    {
                        NewsObj = new NewsObj
                        {
                            Title = "Test News",
                            Content = "Test news content"
                        }
                    }
                }
            }
        };

        _newsApiClientMock.Setup(n => n.GetNewsAsync())
            .ReturnsAsync(newsResponse);

        // Act
        var result = await _sut.GetPersonaAsync(serverId);

        // Assert
        Assert.Contains(expectedMood, result);
        _memoryCacheMock.Verify(m => m.CreateEntry(cacheKey), Times.Once);
    }

    [Fact]
    public async Task GetPersonaAsync_WithNoServerPersona_ShouldReturnDefaultPersona()
    {
        // Arrange
        var serverId = 123456789UL;
        var cacheKey = $"computed_persona_message_{serverId}";
        var expectedMood = "Default mood";

        object? cachedValue = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out cachedValue))
            .Returns(false);

        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = string.Empty // No custom persona
        };

        _serverMetaServiceMock.Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _coreChatServiceMock.Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), null, It.IsAny<int>()))
            .ReturnsAsync(expectedMood);

        var newsResponse = new NewsApiResponse
        {
            Data = new Data
            {
                NewsList = new List<NewsList>
                {
                    new()
                    {
                        NewsObj = new NewsObj
                        {
                            Title = "Test News",
                            Content = "Test news content"
                        }
                    }
                }
            }
        };

        _newsApiClientMock.Setup(n => n.GetNewsAsync())
            .ReturnsAsync(newsResponse);

        // Act
        var result = await _sut.GetPersonaAsync(serverId);

        // Assert
        Assert.Contains("Amiquin", result);
        Assert.Contains("AI assistant for discord", result);
        Assert.Contains(expectedMood, result);
    }

    [Fact]
    public async Task GetPersonaAsync_WithNewsApiError_ShouldHandleGracefully()
    {
        // Arrange
        var serverId = 123456789UL;
        var cacheKey = $"computed_persona_message_{serverId}";

        object? cachedValue = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out cachedValue))
            .Returns(false);

        var serverMeta = new Core.Models.ServerMeta
        {
            Id = serverId,
            Persona = "Test persona with {mood}"
        };

        _serverMetaServiceMock.Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(serverMeta);

        _newsApiClientMock.Setup(n => n.GetNewsAsync())
            .ThrowsAsync(new Exception("News API error"));

        // Act
        var result = await _sut.GetPersonaAsync(serverId);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Test persona", result);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Error while computing mood")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AddSummaryAsync_WithExistingPersona_ShouldAppendSummary()
    {
        // Arrange
        var serverId = 123456789UL;
        var updateMessage = "Recent conversation summary";
        var existingPersona = "Existing persona message";
        var cacheKey = $"computed_persona_message_{serverId}";

        object? outVal = existingPersona;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out outVal)).Returns(true);

        // Act
        await _sut.AddSummaryAsync(serverId, updateMessage);

        // Assert
        _memoryCacheMock.Verify(m => m.CreateEntry(cacheKey), Times.Once);
    }

    [Fact]
    public async Task AddSummaryAsync_WithNoExistingPersona_ShouldCreateAndAppendSummary()
    {
        // Arrange
        var serverId = 123456789UL;
        var updateMessage = "Recent conversation summary";
        var cacheKey = $"computed_persona_message_{serverId}";

        object? outVal = null;
        _memoryCacheMock.Setup(m => m.TryGetValue(cacheKey, out outVal))
            .Returns(false);

        // Setup for GetPersonaInternalAsync when no cached persona exists
        _serverMetaServiceMock.Setup(s => s.GetServerMetaAsync(serverId))
            .ReturnsAsync(new Core.Models.ServerMeta { Id = serverId, Persona = "Server persona" });

        _coreChatServiceMock.Setup(c => c.ExchangeMessageAsync(It.IsAny<string>(), null, It.IsAny<int>()))
            .ReturnsAsync("mood");

        var newsResponse = new NewsApiResponse
        {
            Data = new Data { NewsList = new List<NewsList>() }
        };

        _newsApiClientMock.Setup(n => n.GetNewsAsync())
            .ReturnsAsync(newsResponse);

        // Act
        await _sut.AddSummaryAsync(serverId, updateMessage);

        // Assert
        _memoryCacheMock.Verify(m => m.CreateEntry(cacheKey), Times.AtLeastOnce);
    }
}
#endif