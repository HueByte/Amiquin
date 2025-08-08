namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Defines the contract for Discord bot chat services.
/// </summary>
public interface IDiscordBotChatService
{
    /// <summary>
    /// Processes a Discord message and returns the AI response with full session management.
    /// </summary>
    /// <param name="userMessage">The user's message content</param>
    /// <param name="userId">Discord user ID for session management</param>
    /// <param name="channelId">Discord channel ID for context separation</param>
    /// <param name="guildId">Discord guild ID (optional, for server-specific sessions)</param>
    /// <returns>AI response string</returns>
    Task<string> ProcessMessageAsync(string userMessage, ulong userId, ulong channelId, ulong? guildId = null);

    /// <summary>
    /// Processes a simple message without session management (stateless).
    /// </summary>
    /// <param name="userMessage">The user's message content</param>
    /// <returns>AI response string</returns>
    Task<string> ProcessSimpleMessageAsync(string userMessage);

    /// <summary>
    /// Clears conversation history for a specific user/channel combination.
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="channelId">Discord channel ID</param>
    /// <param name="guildId">Discord guild ID (optional)</param>
    Task ClearConversationAsync(ulong userId, ulong channelId, ulong? guildId = null);

    /// <summary>
    /// Gets conversation statistics for a session.
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="channelId">Discord channel ID</param>
    /// <param name="guildId">Discord guild ID (optional)</param>
    /// <returns>Session statistics</returns>
    Task<SessionStats> GetConversationStatsAsync(ulong userId, ulong channelId, ulong? guildId = null);
}