namespace Amiquin.Core.Models;

public class NsfwImage
{
    public string Url { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Tags { get; set; }
    public string? Artist { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
}