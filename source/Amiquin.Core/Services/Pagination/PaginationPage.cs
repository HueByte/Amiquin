using Discord;

namespace Amiquin.Core.Services.Pagination;

/// <summary>
/// Represents a single page of paginated content for ComponentsV2
/// </summary>
public class PaginationPage
{
    /// <summary>
    /// The title of the page
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The main content of the page
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional sections with additional formatted content
    /// </summary>
    public List<PageSection> Sections { get; set; } = new();

    /// <summary>
    /// Optional thumbnail URL
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Optional image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Optional color for visual accent
    /// </summary>
    public Color? Color { get; set; }

    /// <summary>
    /// Timestamp for the page
    /// </summary>
    public DateTimeOffset? Timestamp { get; set; }

    /// <summary>
    /// Creates a new pagination page
    /// </summary>
    public PaginationPage() { }

    /// <summary>
    /// Creates a new pagination page with content
    /// </summary>
    public PaginationPage(string content)
    {
        Content = content;
    }

    /// <summary>
    /// Creates a pagination page from an embed (for migration purposes)
    /// </summary>
    public static PaginationPage FromEmbed(Embed embed)
    {
        var page = new PaginationPage
        {
            Title = embed.Title,
            Content = embed.Description ?? string.Empty,
            ThumbnailUrl = embed.Thumbnail?.Url,
            ImageUrl = embed.Image?.Url,
            Color = embed.Color,
            Timestamp = embed.Timestamp
        };

        // Convert author to a section
        if (embed.Author.HasValue)
        {
            page.Sections.Add(new PageSection
            {
                Title = "Author",
                Content = embed.Author.Value.Name,
                IconUrl = embed.Author.Value.IconUrl
            });
        }

        // Convert fields to sections
        foreach (var field in embed.Fields)
        {
            page.Sections.Add(new PageSection
            {
                Title = field.Name,
                Content = field.Value,
                IsInline = field.Inline
            });
        }

        return page;
    }
}

/// <summary>
/// Represents a section within a pagination page
/// </summary>
public class PageSection
{
    /// <summary>
    /// The title of the section
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The content of the section
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional icon URL for the section
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Whether this section should be displayed inline
    /// </summary>
    public bool IsInline { get; set; }
}