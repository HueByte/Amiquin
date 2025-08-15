using Amiquin.Core.Services.Scrappers.Models;

namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for scrapper providers
/// </summary>
public class ScrapperOptions : IOption
{
    public const string SectionName = "Scrappers";

    /// <summary>
    /// Configuration for Luscious.net scrapper
    /// </summary>
    public ScrapperProviderOptions Luscious { get; set; } = new();
}

/// <summary>
/// Individual scrapper provider configuration
/// </summary>
public class ScrapperProviderOptions
{
    /// <summary>
    /// The name of the scrapper source
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// The base URL of the scrapper source
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// List of extraction steps to perform in sequence
    /// </summary>
    public List<ScrapperStep> ExtractionSteps { get; set; } = new();

    /// <summary>
    /// HTTP request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// User agent string to use for requests
    /// </summary>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

    /// <summary>
    /// Whether the scrapper is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}