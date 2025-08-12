using Amiquin.Core.Abstraction;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Amiquin.Core.Models;

/// <summary>
/// Represents a single message in a chat session.
/// </summary>
[Table("SessionMessages")]
public class SessionMessage : DbModel<string>
{
    /// <summary>
    /// Foreign key to the chat session
    /// </summary>
    [Required]
    public string ChatSessionId { get; set; } = string.Empty;

    /// <summary>
    /// Discord Message ID
    /// </summary>
    [MaxLength(20)]
    public string? DiscordMessageId { get; set; }

    /// <summary>
    /// Role of the message sender (user, assistant, system)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Content of the message
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Estimated token count for this message
    /// </summary>
    public int EstimatedTokens { get; set; } = 0;

    /// <summary>
    /// Whether this message should be included in context
    /// </summary>
    public bool IncludeInContext { get; set; } = true;

    /// <summary>
    /// JSON metadata for additional message data
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation property
    public virtual ChatSession ChatSession { get; set; } = default!;
}