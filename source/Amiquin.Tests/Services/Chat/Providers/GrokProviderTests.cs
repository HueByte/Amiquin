using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Amiquin.Tests.Services.Chat.Providers;

public class GrokProviderTests
{
    private readonly Mock<ILogger<GrokProvider>> _loggerMock;
    private readonly Mock<IOptions<GrokOptions>> _optionsMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly GrokProvider _sut;
    
    public GrokProviderTests()
    {
        _loggerMock = new Mock<ILogger<GrokProvider>>();
        _optionsMock = new Mock<IOptions<GrokOptions>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        var options = new GrokOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.x.ai/v1/",
            Model = "grok-beta",
            Enabled = true
        };
        
        _optionsMock.Setup(x => x.Value).Returns(options);
        
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri(options.BaseUrl)
        };
        
        _httpClientFactoryMock.Setup(x => x.CreateClient("Grok"))
            .Returns(_httpClient);
        
        _sut = new GrokProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            _optionsMock.Object
        );
    }
    
    [Fact]
    public void ProviderName_ShouldReturnGrok()
    {
        // Assert
        Assert.Equal("Grok", _sut.ProviderName);
    }
    
    [Fact]
    public void MaxContextTokens_ShouldReturnCorrectValue()
    {
        // Assert
        Assert.Equal(131072, _sut.MaxContextTokens);
    }
    
    [Fact]
    public async Task ChatAsync_WithValidMessages_ShouldReturnResponse()
    {
        // Arrange
        var messages = new List<SessionMessage>
        {
            new SessionMessage { Role = "system", Content = "You are helpful.", Id = "1" },
            new SessionMessage { Role = "user", Content = "Hello", Id = "2" }
        };
        
        var options = new ChatCompletionOptions
        {
            MaxTokens = 100,
            Temperature = 0.7f
        };
        
        var mockResponse = new
        {
            id = "chat-123",
            model = "grok-beta",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = "Hello! How can I help?" },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 10,
                completion_tokens = 5,
                total_tokens = 15
            }
        };
        
        var responseJson = JsonSerializer.Serialize(mockResponse);
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
        
        // Act
        var result = await _sut.ChatAsync(messages, options);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello! How can I help?", result.Content);
        Assert.Equal("assistant", result.Role);
        Assert.Equal(10, result.PromptTokens);
        Assert.Equal(5, result.CompletionTokens);
        Assert.Equal(15, result.TotalTokens);
    }
    
    [Fact]
    public async Task ChatAsync_WithApiError_ShouldThrowException()
    {
        // Arrange
        var messages = new List<SessionMessage>
        {
            new SessionMessage { Role = "user", Content = "Hello", Id = "1" }
        };
        
        var options = new ChatCompletionOptions();
        
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent("Bad Request", Encoding.UTF8, "application/json")
        };
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
        
        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.ChatAsync(messages, options));
    }
    
    [Fact]
    public async Task IsAvailableAsync_WithValidApiKey_ShouldReturnTrue()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        };
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
        
        // Act
        var result = await _sut.IsAvailableAsync();
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public async Task IsAvailableAsync_WithInvalidApiKey_ShouldReturnFalse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.Unauthorized,
            Content = new StringContent("Unauthorized", Encoding.UTF8, "application/json")
        };
        
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(httpResponse);
        
        // Act
        var result = await _sut.IsAvailableAsync();
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsAvailableAsync_WithNoApiKey_ShouldReturnFalse()
    {
        // Arrange
        var options = new GrokOptions
        {
            ApiKey = string.Empty,
            BaseUrl = "https://api.x.ai/v1/",
            Model = "grok-beta"
        };
        
        var optionsMock = new Mock<IOptions<GrokOptions>>();
        optionsMock.Setup(x => x.Value).Returns(options);
        
        var provider = new GrokProvider(
            _loggerMock.Object,
            _httpClientFactoryMock.Object,
            optionsMock.Object
        );
        
        // Act
        var result = await provider.IsAvailableAsync();
        
        // Assert
        Assert.False(result);
    }
}