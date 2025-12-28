using Amiquin.Core.Models;
using Amiquin.Core.Services.Nsfw;
using Amiquin.Core.Services.Nsfw.Providers;
using Amiquin.Core.Services.Scrappers;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace Amiquin.Tests.Services.Nsfw;

public class NsfwApiServiceTests : IDisposable
{
    private readonly Mock<ILogger<NsfwApiService>> _loggerMock;
    private readonly Mock<INsfwProvider> _mockProvider;
    private readonly Mock<IScrapperManagerService> _mockScrapperManager;
    private readonly NsfwApiService _nsfwApiService;

    public NsfwApiServiceTests()
    {
        // Reset static state before each test
        ResetStaticRateLimitState();

        _loggerMock = new Mock<ILogger<NsfwApiService>>();
        _mockProvider = new Mock<INsfwProvider>();
        _mockScrapperManager = new Mock<IScrapperManagerService>();

        // Setup mock provider
        _mockProvider.Setup(p => p.Name).Returns("TestProvider");
        _mockProvider.Setup(p => p.IsAvailable).Returns(true);

        // Setup mock scrapper manager - returns empty list by default so providers are used
        _mockScrapperManager.Setup(m => m.GetDataScrapers()).Returns(new List<IDataScrapper>());

        var providers = new List<INsfwProvider> { _mockProvider.Object };
        _nsfwApiService = new NsfwApiService(_loggerMock.Object, providers, _mockScrapperManager.Object);
    }

    public void Dispose()
    {
        // Reset static state after each test to ensure isolation
        ResetStaticRateLimitState();
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
    public async Task GetWaifuImagesAsync_ReturnsImagesFromProvider()
    {
        // Arrange - Use provider name "waifu" which is the preferred provider for GetWaifuImagesAsync
        _mockProvider.Setup(p => p.Name).Returns("waifu");

        var expectedImage = new NsfwImage
        {
            Url = "https://example.com/image1.jpg",
            Source = "waifu",
            Tags = "test",
            Width = 1920,
            Height = 1080
        };

        _mockProvider
            .Setup(p => p.FetchImageAsync())
            .ReturnsAsync(expectedImage);

        // Act
        var result = await _nsfwApiService.GetWaifuImagesAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("https://example.com/image1.jpg", result[0].Url);
        Assert.Equal("waifu", result[0].Source);

        // Verify the provider was called
        _mockProvider.Verify(p => p.FetchImageAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetWaifuImagesAsync_HandlesUnavailableProvider()
    {
        // Arrange
        _mockProvider.Setup(p => p.IsAvailable).Returns(false);

        // Act
        var result = await _nsfwApiService.GetWaifuImagesAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify the provider was not called because it's unavailable
        _mockProvider.Verify(p => p.FetchImageAsync(), Times.Never);
    }

    [Fact]
    public async Task GetNsfwImagesWithStatusAsync_ReturnsSuccessWithImages()
    {
        // Arrange
        var expectedImage = new NsfwImage
        {
            Url = "https://example.com/image1.jpg",
            Source = "TestProvider",
            Tags = "test"
        };

        _mockProvider
            .Setup(p => p.FetchImageAsync())
            .ReturnsAsync(expectedImage);

        // Act
        var result = await _nsfwApiService.GetNsfwImagesWithStatusAsync(1, 1);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Images);
        Assert.Null(result.ErrorMessage);
        Assert.False(result.IsRateLimited);
    }

    [Fact]
    public async Task GetAlternativeNsfwImagesAsync_ReturnsImagesFromProviders()
    {
        // Arrange
        var expectedImage = new NsfwImage
        {
            Url = "https://example.com/image1.jpg",
            Source = "TestProvider",
            Tags = "test"
        };

        _mockProvider
            .Setup(p => p.FetchImageAsync())
            .ReturnsAsync(expectedImage);

        // Act
        var result = await _nsfwApiService.GetAlternativeNsfwImagesAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("https://example.com/image1.jpg", result[0].Url);
    }
}