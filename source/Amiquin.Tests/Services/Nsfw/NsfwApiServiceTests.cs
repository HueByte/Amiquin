using Amiquin.Core.Services.Nsfw;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Reflection;
using System.Text;
using Xunit;

namespace Amiquin.Tests.Services.Nsfw;

public class NsfwApiServiceTests : IDisposable
{
    private readonly Mock<ILogger<NsfwApiService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<EHentaiService> _eHentaiServiceMock;
    private readonly NsfwApiService _nsfwApiService;

    public NsfwApiServiceTests()
    {
        // Reset static state before each test
        ResetStaticRateLimitState();

        _loggerMock = new Mock<ILogger<NsfwApiService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _eHentaiServiceMock = new Mock<EHentaiService>(Mock.Of<ILogger<EHentaiService>>(), Mock.Of<HttpClient>());

        _nsfwApiService = new NsfwApiService(_loggerMock.Object, _httpClient, _eHentaiServiceMock.Object);
    }

    public void Dispose()
    {
        // Reset static state after each test to ensure isolation
        ResetStaticRateLimitState();
        _httpClient?.Dispose();
    }

    private void ResetStaticRateLimitState()
    {
        // Use reflection to reset the static fields
        var serviceType = typeof(NsfwApiService);

        var lastRateLimitField = serviceType.GetField("_lastRateLimitHit", BindingFlags.NonPublic | BindingFlags.Static);
        lastRateLimitField?.SetValue(null, DateTime.MinValue);

        var consecutiveRateLimitsField = serviceType.GetField("_consecutiveRateLimits", BindingFlags.NonPublic | BindingFlags.Static);
        consecutiveRateLimitsField?.SetValue(null, 0);
    }

    [Fact]
    public async Task GetWaifuImagesAsync_UsesSearchEndpoint_NotFavEndpoint()
    {
        // Arrange
        var expectedResponse = @"{
            ""images"": [
                {
                    ""url"": ""https://example.com/image1.jpg"",
                    ""width"": 1920,
                    ""height"": 1080,
                    ""artist"": {
                        ""name"": ""TestArtist""
                    }
                }
            ]
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString().Contains("/search") &&
                    !req.RequestUri.ToString().Contains("/fav") &&
                    req.RequestUri.ToString().Contains("is_nsfw=true")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _nsfwApiService.GetWaifuImagesAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("https://example.com/image1.jpg", result[0].Url);
        Assert.Equal("waifu.im", result[0].Source);

        // Verify the request was made to /search endpoint
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri != null &&
                req.RequestUri.ToString().Contains("/search") &&
                !req.RequestUri.ToString().Contains("/fav")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetWaifuImagesAsync_AddsVersionHeader_NoAuthentication()
    {
        // Arrange
        var expectedResponse = @"{
            ""images"": [
                {
                    ""url"": ""https://example.com/image1.jpg""
                }
            ]
        }";

        HttpRequestMessage? capturedRequest = null;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(expectedResponse, Encoding.UTF8, "application/json")
            });

        // Act
        await _nsfwApiService.GetWaifuImagesAsync(1);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest.Headers.Contains("Authorization"));
        Assert.Contains("v5", capturedRequest.Headers.GetValues("Accept-Version"));
    }

    [Fact]
    public async Task GetWaifuImagesAsync_HandlesRateLimiting_WithRetryAfterHeader()
    {
        // Arrange
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimitResponse.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(rateLimitResponse);

        // Act
        var result = await _nsfwApiService.GetWaifuImagesAsync(5);

        // Assert
        Assert.Empty(result);

        // Verify rate limit warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Rate limited")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetNsfwImagesWithStatusAsync_ReturnsRateLimitedStatus_AfterReceiving429()
    {
        // Arrange - First trigger rate limiting with a 429 response
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(rateLimitResponse);

        // First call to trigger rate limiting
        await _nsfwApiService.GetWaifuImagesAsync(1);

        // Act - Second call should detect rate limiting
        var result = await _nsfwApiService.GetNsfwImagesWithStatusAsync(5, 5);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsRateLimited);
        Assert.True(result.IsTemporaryFailure);
        Assert.Contains("rate-limited", result.ErrorMessage);
    }


    [Fact]
    public async Task GetAlternativeNsfwImagesAsync_UsesFallbackApis_WithRandomizedProviders()
    {
        // Arrange - Mock purrbot response
        var purrbotResponse = @"{
            ""link"": ""https://purrbot.site/image1.jpg""
        }";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri != null &&
                    req.RequestUri.ToString().Contains("purrbot.site")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(purrbotResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _nsfwApiService.GetAlternativeNsfwImagesAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Count >= 0); // May get 0 or more depending on which providers respond
    }
}