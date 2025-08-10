using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Amiquin.Tests.Services.Chat;

/// <summary>
/// Simple integration tests for chat services
/// </summary>
public class SimpleChatServiceTests
{
    [Fact]
    public async Task CoreChatService_Should_BuildSystemMessage_Correctly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CoreChatService>>();
        var mockProviderFactory = new Mock<IChatProviderFactory>();
        var mockMessageCache = new Mock<IMessageCacheService>();
        var mockSemaphoreManager = new Mock<ISemaphoreManager>();
        var mockProvider = new Mock<IChatProvider>();
        
        var llmOptions = new LLMOptions
        {
            GlobalSystemMessage = "You are a helpful AI assistant",
            DefaultProvider = "OpenAI",
            FallbackOrder = ["OpenAI"]
        };
        var mockOptions = new Mock<IOptions<LLMOptions>>();
        mockOptions.Setup(x => x.Value).Returns(llmOptions);

        mockMessageCache
            .Setup(x => x.GetPersonaCoreMessageAsync())
            .ReturnsAsync("Base persona");

        mockProviderFactory
            .Setup(x => x.GetProvider("OpenAI"))
            .Returns(mockProvider.Object);

        mockProvider
            .Setup(x => x.IsAvailableAsync())
            .ReturnsAsync(true);

        mockProvider
            .Setup(x => x.ChatAsync(It.IsAny<List<SessionMessage>>(), It.IsAny<ChatCompletionOptions>()))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Test response" });

        var service = new CoreChatService(
            mockLogger.Object,
            mockProviderFactory.Object,
            mockMessageCache.Object,
            mockSemaphoreManager.Object,
            mockOptions.Object);

        // Act
        var result = await service.CoreRequestAsync("Hello", "Custom persona");

        // Assert
        Assert.Equal("Test response", result.Content);
        
        // Verify the system message contains both base and custom persona
        mockProvider.Verify(x => x.ChatAsync(
            It.Is<List<SessionMessage>>(msgs => 
                msgs.Count == 2 && 
                msgs[0].Role == "system" && 
                msgs[0].Content.Contains("Base persona") &&
                msgs[0].Content.Contains("Custom persona")), 
            It.IsAny<ChatCompletionOptions>()), Times.Once);
    }

    [Fact]
    public async Task CoreChatService_Should_HandleProviderFailure()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CoreChatService>>();
        var mockProviderFactory = new Mock<IChatProviderFactory>();
        var mockMessageCache = new Mock<IMessageCacheService>();
        var mockSemaphoreManager = new Mock<ISemaphoreManager>();
        
        var llmOptions = new LLMOptions
        {
            GlobalSystemMessage = "You are a helpful AI assistant",
            DefaultProvider = "OpenAI",
            FallbackOrder = ["OpenAI"],
            EnableFallback = false
        };
        var mockOptions = new Mock<IOptions<LLMOptions>>();
        mockOptions.Setup(x => x.Value).Returns(llmOptions);

        mockMessageCache
            .Setup(x => x.GetPersonaCoreMessageAsync())
            .ReturnsAsync("Base persona");

        mockProviderFactory
            .Setup(x => x.GetProvider("OpenAI"))
            .Throws(new Exception("Provider not found"));

        var service = new CoreChatService(
            mockLogger.Object,
            mockProviderFactory.Object,
            mockMessageCache.Object,
            mockSemaphoreManager.Object,
            mockOptions.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(() => service.CoreRequestAsync("Hello"));
        Assert.Contains("Provider not found", exception.Message);
    }

    [Fact] 
    public async Task PersonaChatService_Should_HandleBasicFlow()
    {
        // This is a simplified test that verifies the basic structure without complex mocking
        var mockLogger = new Mock<ILogger<PersonaChatService>>();
        var mockCoreChatService = new Mock<IChatCoreService>();
        var mockMessageCache = new Mock<IMessageCacheService>();
        var mockServerMetaService = new Mock<IServerMetaService>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        
        var botOptions = new BotOptions { Name = "TestBot", MaxTokens = 4000 };
        var mockBotOptions = new Mock<IOptions<BotOptions>>();
        mockBotOptions.Setup(x => x.Value).Returns(botOptions);

        // Setup basic successful response
        mockCoreChatService
            .Setup(x => x.CoreRequestAsync(It.IsAny<string>(), It.IsAny<string>(), 1200, null))
            .ReturnsAsync(new ChatCompletionResponse { Content = "Test response" });

        var service = new PersonaChatService(
            mockLogger.Object,
            mockCoreChatService.Object,
            mockMessageCache.Object,
            mockServerMetaService.Object,
            mockServiceProvider.Object,
            mockBotOptions.Object);

        // Act
        var result = await service.ExchangeMessageAsync(12345, "Hello");

        // Assert
        Assert.Equal("Test response", result);
    }
}