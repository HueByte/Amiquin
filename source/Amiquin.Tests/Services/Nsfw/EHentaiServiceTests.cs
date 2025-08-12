using Amiquin.Core.Services.Nsfw;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using Xunit;

namespace Amiquin.Tests.Services.Nsfw;

public class EHentaiServiceTests : IDisposable
{
    private readonly Mock<ILogger<EHentaiService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly EHentaiService _eHentaiService;

    public EHentaiServiceTests()
    {
        _loggerMock = new Mock<ILogger<EHentaiService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);

        _eHentaiService = new EHentaiService(_loggerMock.Object, _httpClient);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    [Fact]
    public async Task ScrapeGalleryLinksAsync_ParsesGalleryLinksFromHtml()
    {
        // Arrange
        var mockHtml = @"
            <html>
                <body>
                    <div class='gl3t'>
                        <a href='https://e-hentai.org/g/2231376/a7584a5932/'>Gallery 1</a>
                    </div>
                    <div class='gl3t'>
                        <a href='https://e-hentai.org/g/1234567/abcdef123/'>Gallery 2</a>
                    </div>
                </body>
            </html>";

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockHtml, Encoding.UTF8, "text/html")
            });

        // Act
        var result = await _eHentaiService.ScrapeGalleryLinksAsync("test", 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var firstGallery = result.FirstOrDefault(g => g.GalleryId == "2231376");
        Assert.NotNull(firstGallery);
        Assert.Equal("a7584a5932", firstGallery.GalleryToken);
        Assert.Equal("https://e-hentai.org/g/2231376/a7584a5932/", firstGallery.Url);

        var secondGallery = result.FirstOrDefault(g => g.GalleryId == "1234567");
        Assert.NotNull(secondGallery);
        Assert.Equal("abcdef123", secondGallery.GalleryToken);
        Assert.Equal("https://e-hentai.org/g/1234567/abcdef123/", secondGallery.Url);
    }

    [Fact]
    public async Task FetchGalleryMetadataAsync_CallsApiWithCorrectRequest()
    {
        // Arrange
        var mockApiResponse = @"{
            ""gmetadata"": [
                {
                    ""gid"": 2231376,
                    ""token"": ""a7584a5932"",
                    ""title"": ""Test Gallery"",
                    ""title_jpn"": ""テストギャラリー"",
                    ""category"": ""Doujinshi"",
                    ""thumb"": ""https://example.com/thumb.jpg"",
                    ""uploader"": ""TestUploader"",
                    ""filecount"": ""25"",
                    ""rating"": ""4.5"",
                    ""tags"": [""tag1:value1"", ""tag2:value2""],
                    ""filesize"": 1024000,
                    ""posted"": ""1609459200""
                }
            ]
        }";

        var galleryLinks = new List<Core.Models.EHentai.EHentaiGalleryLink>
        {
            new() { GalleryId = "2231376", GalleryToken = "a7584a5932" }
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("api.e-hentai.org")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockApiResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _eHentaiService.FetchGalleryMetadataAsync(galleryLinks);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var gallery = result[0];
        Assert.Equal(2231376, gallery.GalleryId);
        Assert.Equal("a7584a5932", gallery.Token);
        Assert.Equal("Test Gallery", gallery.Title);
        Assert.Equal("テストギャラリー", gallery.TitleJapanese);
        Assert.Equal("Doujinshi", gallery.Category);
        Assert.Equal("https://example.com/thumb.jpg", gallery.Thumbnail);
        Assert.Equal("TestUploader", gallery.Uploader);
        Assert.Equal("25", gallery.FileCount);
    }

    [Fact]
    public async Task GetRandomGalleriesAsync_ReturnsExpectedCount()
    {
        // Arrange
        var mockHtml = @"
            <html>
                <body>
                    <div class='gl3t'>
                        <a href='https://e-hentai.org/g/2231376/a7584a5932/'>Gallery 1</a>
                    </div>
                    <div class='gl3t'>
                        <a href='https://e-hentai.org/g/1234567/abcdef123/'>Gallery 2</a>
                    </div>
                    <div class='gl3t'>
                        <a href='https://e-hentai.org/g/7654321/fedcba987/'>Gallery 3</a>
                    </div>
                </body>
            </html>";

        var mockApiResponse = @"{
            ""gmetadata"": [
                {
                    ""gid"": 2231376,
                    ""token"": ""a7584a5932"",
                    ""title"": ""Test Gallery 1"",
                    ""title_jpn"": """",
                    ""category"": ""Doujinshi"",
                    ""thumb"": ""https://example.com/thumb1.jpg"",
                    ""uploader"": ""TestUploader1"",
                    ""filecount"": ""25"",
                    ""rating"": ""4.5"",
                    ""tags"": [],
                    ""filesize"": 1024000,
                    ""posted"": ""1609459200""
                },
                {
                    ""gid"": 1234567,
                    ""token"": ""abcdef123"",
                    ""title"": ""Test Gallery 2"",
                    ""title_jpn"": """",
                    ""category"": ""Manga"",
                    ""thumb"": ""https://example.com/thumb2.jpg"",
                    ""uploader"": ""TestUploader2"",
                    ""filecount"": ""30"",
                    ""rating"": ""4.2"",
                    ""tags"": [],
                    ""filesize"": 2048000,
                    ""posted"": ""1609459300""
                }
            ]
        }";

        _httpMessageHandlerMock
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockHtml, Encoding.UTF8, "text/html")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(mockApiResponse, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _eHentaiService.GetRandomGalleriesAsync(2, "test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.All(result, gallery => Assert.NotNull(gallery.Title));
        Assert.All(result, gallery => Assert.NotEmpty(gallery.Thumbnail));
    }
}