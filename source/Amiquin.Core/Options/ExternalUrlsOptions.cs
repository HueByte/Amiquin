namespace Amiquin.Core.Options;

public class ExternalUrlsOptions : IOption
{
    public const string ExternalUrls = "ExternalUrls";
    public string NewsApiUrl { get; set; } = default!;
}