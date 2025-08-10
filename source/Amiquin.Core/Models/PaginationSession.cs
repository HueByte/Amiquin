using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

/// <summary>
/// Represents a pagination session for Discord embed interactions
/// </summary>
[Table("PaginationSessions")]
public class PaginationSession : DbModel<string>
{
    /// <summary>
    /// Discord User ID who can interact with this pagination
    /// </summary>
    [Required]
    public ulong UserId { get; set; }

    /// <summary>
    /// Current page index (0-based)
    /// </summary>
    public int CurrentPage { get; set; } = 0;

    /// <summary>
    /// When the session was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the session expires and should be cleaned up
    /// </summary>
    [Required]
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Discord Guild ID where the pagination is active (nullable for DMs)
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Discord Channel ID where the pagination is active
    /// </summary>
    [Required]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Discord Message ID of the paginated message
    /// </summary>
    [Required]
    public ulong MessageId { get; set; }

    /// <summary>
    /// JSON-serialized array of embed data for pagination
    /// </summary>
    [Required]
    public string EmbedData { get; set; } = default!;

    /// <summary>
    /// Total number of pages in this session
    /// </summary>
    [Required]
    public int TotalPages { get; set; }

    /// <summary>
    /// Type of pagination content (for categorization/debugging)
    /// </summary>
    [MaxLength(50)]
    public string? ContentType { get; set; }

    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creates a new pagination session
    /// </summary>
    /// <param name="userId">Discord User ID</param>
    /// <param name="guildId">Discord Guild ID (nullable for DMs)</param>
    /// <param name="channelId">Discord Channel ID</param>
    /// <param name="messageId">Discord Message ID</param>
    /// <param name="embedData">JSON-serialized embed data</param>
    /// <param name="totalPages">Total number of pages</param>
    /// <param name="timeout">Session timeout duration</param>
    /// <param name="contentType">Type of content being paginated</param>
    /// <returns>A new PaginationSession instance</returns>
    public static PaginationSession CreateSession(
        ulong userId,
        ulong? guildId,
        ulong channelId,
        ulong messageId,
        string embedData,
        int totalPages,
        TimeSpan timeout,
        string? contentType = null)
    {
        return new PaginationSession
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId,
            EmbedData = embedData,
            TotalPages = totalPages,
            ContentType = contentType,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(timeout),
            IsActive = true
        };
    }

    /// <summary>
    /// Checks if the session has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Updates the current page and marks the session as accessed
    /// </summary>
    /// <param name="newPage">New page index</param>
    public void UpdatePage(int newPage)
    {
        if (newPage < 0 || newPage >= TotalPages)
            throw new ArgumentOutOfRangeException(nameof(newPage), "Page index out of range");

        CurrentPage = newPage;
    }

    /// <summary>
    /// Extends the session expiry time
    /// </summary>
    /// <param name="additionalTime">Additional time to add</param>
    public void ExtendExpiry(TimeSpan additionalTime)
    {
        ExpiresAt = DateTime.UtcNow.Add(additionalTime);
    }

    /// <summary>
    /// Deactivates the session
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}