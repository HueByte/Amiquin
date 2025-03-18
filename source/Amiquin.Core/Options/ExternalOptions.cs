namespace Amiquin.Core.Options;

public class ExternalOptions : IOption
{
    public const string External = "External";
    public string NewsApiUrl { get; set; } = default!;
    public string PiperCommand { get; set; } = default!;
    public string ModelName { get; set; } = default!;
}