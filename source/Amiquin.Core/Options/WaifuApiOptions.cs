namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for the Waifu.im API.
/// </summary>
public class WaifuApiOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "WaifuApi";

    /// <summary>
    /// API authentication token obtained from https://www.waifu.im/dashboard/
    /// Optional but recommended for better rate limits and access to authenticated endpoints.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Base URL for the Waifu.im API.
    /// Default: "https://api.waifu.im"
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.waifu.im";

    /// <summary>
    /// API version to use.
    /// Default: "v5"
    /// </summary>
    public string Version { get; set; } = "v5";

    /// <summary>
    /// Whether the Waifu API integration is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets whether authentication is configured (token is not null or empty).
    /// </summary>
    public bool HasAuthentication => !string.IsNullOrWhiteSpace(Token);
}