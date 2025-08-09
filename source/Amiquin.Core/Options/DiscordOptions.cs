namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for Discord bot functionality.
/// </summary>
public class DiscordOptions
{
    public const string SectionName = "Discord";
    
    /// <summary>
    /// Discord bot token for authentication.
    /// </summary>
    public string Token { get; set; } = string.Empty;
    
    /// <summary>
    /// Command prefix for text commands.
    /// </summary>
    public string Prefix { get; set; } = "!amq";
    
    /// <summary>
    /// Activity message displayed in Discord.
    /// </summary>
    public string ActivityMessage { get; set; } = "Chatting with AI";
    
    /// <summary>
    /// Guild ID for command registration (null for global commands).
    /// </summary>
    public ulong? CommandsGuildId { get; set; }
}