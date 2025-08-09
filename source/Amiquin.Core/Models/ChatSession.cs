using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

/// <summary>
/// Defines the scope level for a chat session
/// </summary>
public enum SessionScope
{
    /// <summary>
    /// Session is scoped to a specific user across all channels/servers
    /// </summary>
    User = 0,
    
    /// <summary>
    /// Session is scoped to a specific channel (all users in that channel share context)
    /// </summary>
    Channel = 1,
    
    /// <summary>
    /// Session is scoped to a specific server (default)
    /// </summary>
    Server = 2
}

/// <summary>
/// Represents a chat session between a user and the bot in a specific channel.
/// </summary>
[Table("ChatSessions")]
public class ChatSession : DbModel<string>
{

    /// <summary>
    /// The ID of the entity that owns this session (UserId for User scope, ChannelId for Channel scope, etc.)
    /// </summary>
    [Required]
    public ulong OwningEntityId { get; set; }

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
    /// The AI provider being used for this session (OpenAI, Gemini, Grok)
    /// </summary>
    [MaxLength(20)]
    public string Provider { get; set; } = "OpenAI";

    /// <summary>
    /// Whether the session is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The scope level for this session (User, Channel, or Server)
    /// </summary>
    [Required]
    public SessionScope Scope { get; set; } = SessionScope.Server;

    /// <summary>
    /// JSON metadata for additional session data
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties
    public virtual ICollection<SessionMessage> Messages { get; set; } = new List<SessionMessage>();
    public virtual ServerMeta? Server { get; set; }

    /// <summary>
    /// Creates a new chat session with the specified scope and context
    /// </summary>
    /// <param name="scope">The scope level for the session</param>
    /// <param name="userId">Discord User ID (for User scope)</param>
    /// <param name="channelId">Discord Channel ID (for Channel scope)</param>
    /// <param name="serverId">Discord Server ID (for Server scope)</param>
    /// <param name="model">AI model to use for this session</param>
    /// <param name="provider">AI provider to use for this session</param>
    /// <returns>A new ChatSession instance</returns>
    public static ChatSession CreateSession(SessionScope scope, ulong userId = 0, ulong channelId = 0, ulong serverId = 0, string model = "gpt-4o-mini", string provider = "OpenAI")
    {
        var owningEntityId = scope switch
        {
            SessionScope.User => userId,
            SessionScope.Channel => channelId,
            SessionScope.Server => serverId, // Server scope is keyed by server/guild ID
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid session scope")
        };

        if (owningEntityId == 0)
        {
            throw new ArgumentException($"Valid {scope} ID is required for {scope} scope");
        }

        return new ChatSession
        {
            Id = Guid.NewGuid().ToString(),
            Scope = scope,
            OwningEntityId = owningEntityId,
            Model = model,
            Provider = provider,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    /// <summary>
    /// Determines if this session matches the given scope and context
    /// </summary>
    public bool MatchesScope(SessionScope scope, ulong userId, ulong channelId, ulong serverId)
    {
        if (Scope != scope)
            return false;

        var expectedOwningEntityId = scope switch
        {
            SessionScope.User => userId,
            SessionScope.Channel => channelId,
            SessionScope.Server => serverId, // Server scope uses server/guild ID as owning entity
            _ => (ulong)0
        };

        return OwningEntityId == expectedOwningEntityId;
    }

    /// <summary>
    /// Gets the appropriate owning entity ID for a given scope and context
    /// </summary>
    public static ulong GetOwningEntityId(SessionScope scope, ulong userId, ulong channelId, ulong serverId)
    {
        return scope switch
        {
            SessionScope.User => userId,
            SessionScope.Channel => channelId,
            SessionScope.Server => serverId, // Server scope uses server/guild ID
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Invalid session scope")
        };
    }
}