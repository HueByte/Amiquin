using System.Text.Json.Serialization;

namespace Amiquin.Core.Models.EHentai;

/// <summary>
/// Represents a gallery link extracted from e-hentai search results
/// </summary>
public class EHentaiGalleryLink
{
    public string GalleryId { get; set; } = string.Empty;
    public string GalleryToken { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Represents the API request structure for e-hentai gallery metadata
/// </summary>
public class EHentaiApiRequest
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "gdata";

    [JsonPropertyName("gidlist")]
    public List<List<object>> GalleryList { get; set; } = new();

    [JsonPropertyName("namespace")]
    public int Namespace { get; set; } = 1;
}

/// <summary>
/// Represents the API response structure for e-hentai gallery metadata
/// </summary>
public class EHentaiApiResponse
{
    [JsonPropertyName("gmetadata")]
    public List<EHentaiGalleryMetadata> GalleryMetadata { get; set; } = new();
}

/// <summary>
/// Represents detailed metadata for an e-hentai gallery
/// </summary>
public class EHentaiGalleryMetadata
{
    [JsonPropertyName("gid")]
    public long GalleryId { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("title_jpn")]
    public string TitleJapanese { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("thumb")]
    public string Thumbnail { get; set; } = string.Empty;

    [JsonPropertyName("uploader")]
    public string Uploader { get; set; } = string.Empty;

    [JsonPropertyName("filecount")]
    public string FileCount { get; set; } = string.Empty;

    [JsonPropertyName("rating")]
    public string Rating { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("filesize")]
    public long FileSize { get; set; }

    [JsonPropertyName("posted")]
    public string Posted { get; set; } = string.Empty;
}