using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Amiquin.Core.Models;

/// <summary>
/// Represents a chat session between a user and the bot in a specific channel.
/// </summary>
[Table("ChatSessions")]
public class ChatSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the session (combines UserId, ChannelId, ServerId)
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Discord User ID who owns this session
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Discord Channel ID where the session takes place
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Discord Server ID (Guild ID) where the session takes place
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ServerId { get; set; } = string.Empty;

    /// <summary>
    /// When the session was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time there was activity in this session
    /// </summary>
    [Required]
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Number of messages exchanged in this session
    /// </summary>
    public int MessageCount { get; set; } = 0;

    /// <summary>
    /// Estimated token count for the session
    /// </summary>
    public int EstimatedTokens { get; set; } = 0;

    /// <summary>
    /// Current AI model being used for this session
    /// </summary>
    [MaxLength(50)]
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// JSON metadata for additional session data
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ICollection<SessionMessage> Messages { get; set; } = new List<SessionMessage>();
    public virtual ServerMeta? Server { get; set; }

    /// <summary>
    /// Generates a session ID from user, channel, and server IDs
    /// </summary>
    public static string GenerateSessionId(string userId, string channelId, string serverId)
    {
        return $"{serverId}_{channelId}_{userId}";
    }
}