namespace Amiquin.Core.Options;

public class ExternalOptions : IOption
{
    public const string External = "External";
    public string NewsApiUrl { get; set; } = default!;
}