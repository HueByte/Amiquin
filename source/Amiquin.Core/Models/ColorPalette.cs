namespace Amiquin.Core.Models;

/// <summary>
/// Represents a color palette with color theory information.
/// </summary>
public class ColorPalette
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ColorHarmonyType HarmonyType { get; set; }
    public float BaseHue { get; set; }
    public List<PaletteColor> Colors { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
    public string? PreviewImageUrl { get; set; }
}

/// <summary>
/// Represents a single color in a palette.
/// </summary>
public class PaletteColor
{
    public string Hex { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public float Hue { get; set; }
    public float Saturation { get; set; }
    public float Lightness { get; set; }
    public ColorRole Role { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Types of color harmony relationships.
/// </summary>
public enum ColorHarmonyType
{
    Monochromatic,
    Analogous,
    Complementary,
    SplitComplementary,
    Triadic,
    Tetradic,
    Square
}

/// <summary>
/// Role of a color within a palette.
/// </summary>
public enum ColorRole
{
    Primary,
    Secondary,
    Accent,
    Neutral,
    Highlight
}