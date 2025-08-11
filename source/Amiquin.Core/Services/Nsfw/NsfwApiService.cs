using System.Text.Json;
using Amiquin.Core.Models;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Nsfw;

public class NsfwApiService : INsfwApiService
{
    private readonly ILogger<NsfwApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();

    private readonly string[] _waifuTags = new[]
    {
        "waifu", "maid", "marin-kitagawa", "mori-calliope", "raiden-shogun",
        "oppai", "selfies", "uniform", "kitsune"
    };

    private readonly string[] _alternativeTags = new[]
    {
        "ass", "hentai", "milf", "oral", "paizuri", "ecchi", "ero"
    };

    public NsfwApiService(ILogger<NsfwApiService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<List<NsfwImage>> GetDailyNsfwImagesAsync(int waifuCount = 5, int otherCount = 5)
    {
        var images = new List<NsfwImage>();

        try
        {
            var waifuTask = GetWaifuImagesAsync(waifuCount);
            var alternativeTask = GetAlternativeNsfwImagesAsync(otherCount);

            await Task.WhenAll(waifuTask, alternativeTask);

            images.AddRange(await waifuTask);
            images.AddRange(await alternativeTask);

            // Shuffle the images for variety
            images = images.OrderBy(x => _random.Next()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching daily NSFW images");
        }

        return images;
    }

    public async Task<List<NsfwImage>> GetWaifuImagesAsync(int count = 5)
    {
        var images = new List<NsfwImage>();

        try
        {
            // Get random tags for variety
            var selectedTags = _waifuTags.OrderBy(x => _random.Next()).Take(count).ToList();

            var tasks = selectedTags.Select(tag => FetchWaifuImageAsync(tag)).ToList();
            var results = await Task.WhenAll(tasks);

            images.AddRange(results.Where(img => img != null)!);

            // If we don't have enough, fetch more with random tags
            while (images.Count < count)
            {
                var randomTag = _waifuTags[_random.Next(_waifuTags.Length)];
                var image = await FetchWaifuImageAsync(randomTag);
                if (image != null)
                {
                    images.Add(image);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching waifu images");
        }

        return images.Take(count).ToList();
    }

    public async Task<List<NsfwImage>> GetAlternativeNsfwImagesAsync(int count = 5)
    {
        var images = new List<NsfwImage>();

        try
        {
            // First try waifu.im with alternative tags
            var selectedTags = _alternativeTags.OrderBy(x => _random.Next()).Take(count).ToList();
            var tasks = selectedTags.Select(tag => FetchWaifuImageAsync(tag)).ToList();
            var results = await Task.WhenAll(tasks);
            images.AddRange(results.Where(img => img != null)!);

            // Try alternative API - waifu.pics NSFW endpoint
            if (images.Count < count)
            {
                var waifuPicsImages = await FetchFromWaifuPicsNsfwAsync(count - images.Count);
                images.AddRange(waifuPicsImages);
            }

            // Try another alternative - nekos.best API
            if (images.Count < count)
            {
                var nekosBestImages = await FetchFromNekosBestAsync(count - images.Count);
                images.AddRange(nekosBestImages);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching alternative NSFW images");
        }

        return images.Take(count).ToList();
    }

    private async Task<NsfwImage?> FetchWaifuImageAsync(string tag)
    {
        try
        {
            var url = $"https://api.waifu.im/search?included_tags={tag}&is_nsfw=true";
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("images", out var imagesElement) &&
                imagesElement.GetArrayLength() > 0)
            {
                var firstImage = imagesElement[0];
                return new NsfwImage
                {
                    Url = firstImage.GetProperty("url").GetString() ?? string.Empty,
                    Source = "waifu.im",
                    Tags = tag,
                    Width = firstImage.TryGetProperty("width", out var width) ? width.GetInt32() : null,
                    Height = firstImage.TryGetProperty("height", out var height) ? height.GetInt32() : null,
                    Artist = firstImage.TryGetProperty("artist", out var artist) && 
                             artist.TryGetProperty("name", out var artistName) 
                             ? artistName.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch image from waifu.im for tag: {Tag}", tag);
        }

        return null;
    }

    private async Task<List<NsfwImage>> FetchFromWaifuPicsNsfwAsync(int count)
    {
        var images = new List<NsfwImage>();
        var categories = new[] { "waifu", "neko", "trap", "blowjob" };

        try
        {
            foreach (var category in categories.Take(count))
            {
                try
                {
                    var url = $"https://api.waifu.pics/nsfw/{category}";
                    var response = await _httpClient.GetStringAsync(url);
                    var jsonDoc = JsonDocument.Parse(response);

                    if (jsonDoc.RootElement.TryGetProperty("url", out var urlElement))
                    {
                        images.Add(new NsfwImage
                        {
                            Url = urlElement.GetString() ?? string.Empty,
                            Source = "waifu.pics",
                            Tags = category
                        });
                    }

                    if (images.Count >= count) break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch from waifu.pics category: {Category}", category);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from waifu.pics");
        }

        return images;
    }

    private async Task<List<NsfwImage>> FetchFromNekosBestAsync(int count)
    {
        var images = new List<NsfwImage>();

        try
        {
            // nekos.best API for hentai content
            var url = $"https://nekos.best/api/v2/hentai?amount={Math.Min(count, 20)}";
            var response = await _httpClient.GetStringAsync(url);
            var jsonDoc = JsonDocument.Parse(response);

            if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElement))
            {
                foreach (var result in resultsElement.EnumerateArray())
                {
                    if (result.TryGetProperty("url", out var urlProp))
                    {
                        images.Add(new NsfwImage
                        {
                            Url = urlProp.GetString() ?? string.Empty,
                            Source = "nekos.best",
                            Tags = "hentai",
                            Artist = result.TryGetProperty("artist_name", out var artist) 
                                     ? artist.GetString() : null,
                            Title = result.TryGetProperty("source_url", out var source) 
                                    ? source.GetString() : null
                        });
                    }

                    if (images.Count >= count) break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching from nekos.best");
        }

        return images;
    }
}