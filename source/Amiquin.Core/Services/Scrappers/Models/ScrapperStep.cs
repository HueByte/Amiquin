namespace Amiquin.Core.Services.Scrappers.Models;

/// <summary>
/// Represents a single step in the scrapping process
/// </summary>
public class ScrapperStep
{
    /// <summary>
    /// Name of this scrapping step for identification and debugging
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The URL to hit for this step. Supports variables like {random(1...100)}
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Regular expression pattern to extract data from the response
    /// </summary>
    public string ExtractionRegex { get; set; } = string.Empty;

    /// <summary>
    /// Which regex capture group to use (default: 1)
    /// </summary>
    public int CaptureGroup { get; set; } = 1;

    /// <summary>
    /// Whether this step should extract multiple matches or just the first one
    /// </summary>
    public bool ExtractMultiple { get; set; } = false;

    /// <summary>
    /// The maximum number of results to return (default: 1)
    /// </summary>
    public int Limit { get; set; } = 1;

    /// <summary>
    /// How many times to repeat this step (default: 1)
    /// </summary>
    public int RepetitionCount { get; set; } = 1;

    /// <summary>
    /// Whether to randomize selection from extracted results (default: false)
    /// If true, will randomly select from all matches instead of taking them in order
    /// </summary>
    public bool RandomizeResults { get; set; } = false;

    /// <summary>
    /// Whether to select the highest resolution image from the matches (default: false)
    /// If true, will parse resolution from URLs and select the one with the highest resolution
    /// </summary>
    public bool SelectHighestResolution { get; set; } = false;
}