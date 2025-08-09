using Amiquin.Core.Models;
using Amiquin.Core.Options.Configuration;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.Chat;

public class MultiProviderChatServiceTests
{
    private readonly Mock<ILogger<MultiProviderChatServiceImpl>> _loggerMock;
    private readonly Mock<IMessageCacheService> _messageCacheServiceMock;
    private readonly Mock<IChatProviderFactory> _providerFactoryMock;
    private readonly Mock<IChatSemaphoreManager> _semaphoreManagerMock;
    private readonly Mock<IOptions<ChatOptions>> _chatOptionsMock;
    private readonly MultiProviderChatServiceImpl _sut;
    
    public MultiProviderChatServiceTests()
    {
        _loggerMock = new Mock<ILogger<MultiProviderChatServiceImpl>>();
        _messageCacheServiceMock = new Mock<IMessageCacheService>();
        _providerFactoryMock = new Mock<IChatProviderFactory>();
        _semaphoreManagerMock = new Mock<IChatSemaphoreManager>();
        _chatOptionsMock = new Mock<IOptions<ChatOptions>>();
        
        var chatOptions = new ChatOptions
        {
            Provider = "OpenAI",
            TokenLimit = 2000,
            Temperature = 0.6f,
            EnableFallback = true,
            FallbackProviders = new List<string> { "OpenAI", "Grok", "Gemini" }
        };
        
        _chatOptionsMock.Setup(x => x.Value).Returns(chatOptions);
        
        // Setup semaphore
        var semaphore = new SemaphoreSlim(1, 1);
        _semaphoreManagerMock.Setup(x => x.GetOrCreateInstanceSemaphore(It.IsAny<ulong>()))
            .Returns(semaphore);
        
        _sut = new MultiProviderChatServiceImpl(
            _loggerMock.Object,
            _messageCacheServiceMock.Object,
            _providerFactoryMock.Object,
            _semaphoreManagerMock.Object,
            _chatOptionsMock.Object
        );
    }
    
    [Fact]
    public async Task ChatAsync_ShouldUseConfiguredProvider()
    {
        // Arrange
        var instanceId = 123456789UL;
        var messages = new List<SessionMessage>
        {
            new SessionMessage { Role = "user", Content = "Hello", Id = "1" }
        };
        
        var mockProvider = new Mock<IChatProvider>();
        mockProvider.Setup(x => x.ProviderName).Returns("OpenAI");
        mockProvider.Setup(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Hi there!", Role = "assistant" });
        
        _providerFactoryMock.Setup(x => x.GetProvider("OpenAI"))
            .Returns(mockProvider.Object);
        
        _messageCacheServiceMock.Setup(x => x.GetPersonaCoreMessageAsync())
            .ReturnsAsync("You are a helpful assistant.");
        
        // Act
        var result = await _sut.ChatAsync(instanceId, messages);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hi there!", result.Content);
        mockProvider.Verify(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()), Times.Once);
    }
    
    [Fact]
    public async Task ChatAsync_WithProviderOverride_ShouldUseSpecifiedProvider()
    {
        // Arrange
        var instanceId = 123456789UL;
        var messages = new List<SessionMessage>
        {
            new SessionMessage { Role = "user", Content = "Hello", Id = "1" }
        };
        
        var mockGrokProvider = new Mock<IChatProvider>();
        mockGrokProvider.Setup(x => x.ProviderName).Returns("Grok");
        mockGrokProvider.Setup(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Grok response", Role = "assistant" });
        
        _providerFactoryMock.Setup(x => x.GetProvider("Grok"))
            .Returns(mockGrokProvider.Object);
        
        _messageCacheServiceMock.Setup(x => x.GetPersonaCoreMessageAsync())
            .ReturnsAsync("You are a helpful assistant.");
        
        // Act
        var result = await _sut.ChatAsync(instanceId, messages, provider: "Grok");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Grok response", result.Content);
        _providerFactoryMock.Verify(x => x.GetProvider("Grok"), Times.Once);
    }
    
    [Fact]
    public async Task ChatAsync_WithProviderFailure_ShouldUseFallback()
    {
        // Arrange
        var instanceId = 123456789UL;
        var messages = new List<SessionMessage>
        {
            new SessionMessage { Role = "user", Content = "Hello", Id = "1" }
        };
        
        var mockOpenAIProvider = new Mock<IChatProvider>();
        mockOpenAIProvider.Setup(x => x.ProviderName).Returns("OpenAI");
        mockOpenAIProvider.Setup(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ThrowsAsync(new HttpRequestException("API Error"));
        
        var mockGrokProvider = new Mock<IChatProvider>();
        mockGrokProvider.Setup(x => x.ProviderName).Returns("Grok");
        mockGrokProvider.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        mockGrokProvider.Setup(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Fallback response", Role = "assistant" });
        
        _providerFactoryMock.Setup(x => x.GetProvider("OpenAI"))
            .Returns(mockOpenAIProvider.Object);
        _providerFactoryMock.Setup(x => x.GetProvider("Grok"))
            .Returns(mockGrokProvider.Object);
        
        _messageCacheServiceMock.Setup(x => x.GetPersonaCoreMessageAsync())
            .ReturnsAsync("You are a helpful assistant.");
        
        // Act
        var result = await _sut.ChatAsync(instanceId, messages);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Fallback response", result.Content);
        mockGrokProvider.Verify(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()), Times.Once);
    }
    
    [Fact]
    public async Task ExchangeMessageAsync_ShouldReturnResponse()
    {
        // Arrange
        var message = "What is the weather?";
        var systemMessage = "You are a weather assistant.";
        
        var mockProvider = new Mock<IChatProvider>();
        mockProvider.Setup(x => x.ProviderName).Returns("OpenAI");
        mockProvider.Setup(x => x.ChatAsync(It.IsAny<IEnumerable<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "The weather is sunny.", Role = "assistant" });
        
        _providerFactoryMock.Setup(x => x.GetProvider("OpenAI"))
            .Returns(mockProvider.Object);
        
        // Act
        var result = await _sut.ExchangeMessageAsync(message, systemMessage, 500);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("The weather is sunny.", result.Content);
    }
    
    [Fact]
    public async Task GetAvailableProviders_ShouldReturnProviderList()
    {
        // Arrange
        var providers = new List<string> { "OpenAI", "Grok", "Gemini" };
        _providerFactoryMock.Setup(x => x.GetAvailableProviders())
            .Returns(providers);
        
        // Act
        var result = _sut.GetAvailableProviders();
        
        // Assert
        Assert.Equal(providers, result);
    }
    
    [Fact]
    public async Task IsProviderAvailableAsync_WithAvailableProvider_ShouldReturnTrue()
    {
        // Arrange
        var mockProvider = new Mock<IChatProvider>();
        mockProvider.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
        
        _providerFactoryMock.Setup(x => x.GetProvider("Grok"))
            .Returns(mockProvider.Object);
        
        // Act
        var result = await _sut.IsProviderAvailableAsync("Grok");
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task IsProviderAvailableAsync_WithUnavailableProvider_ShouldReturnFalse()
    {
        // Arrange
        var mockProvider = new Mock<IChatProvider>();
        mockProvider.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);
        
        _providerFactoryMock.Setup(x => x.GetProvider("Gemini"))
            .Returns(mockProvider.Object);
        
        // Act
        var result = await _sut.IsProviderAvailableAsync("Gemini");
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void GetCurrentProvider_ShouldReturnConfiguredProvider()
    {
        // Act
        var result = _sut.GetCurrentProvider();
        
        // Assert
        Assert.Equal("OpenAI", result);
    }
}