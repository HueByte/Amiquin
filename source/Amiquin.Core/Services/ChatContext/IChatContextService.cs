using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.ChatContext;

/// <summary>
/// Service interface for managing chat context and message handling operations.
/// Provides methods for handling user messages and maintaining conversation context.
/// </summary>
public interface IChatContextService
{
    /// <summary>
    /// Handles a user message received in a Discord channel.
    /// Processes the message and stores it in context for potential AI interactions.
    /// </summary>
    /// <param name="scopeId">The scope identifier (typically server/guild ID) for context grouping.</param>
    /// <param name="socketMessage">The Discord message that was received.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleUserMessageAsync(ulong scopeId, SocketMessage socketMessage);

    /// <summary>
    /// Retrieves and clears the context messages for a specific scope.
    /// This is typically called when the bot needs to process accumulated messages for AI interaction.
    /// </summary>
    /// <param name="scopeId">The scope identifier to retrieve messages for.</param>
    /// <returns>An array of message content strings from the context.</returns>
    string[] GetContextMessages(ulong scopeId);

    #region AI Engagement Actions
    /// <summary>
    /// Starts a new topic or conversation in the channel.
    /// </summary>
    /// <param name="guildId">The guild ID where the topic should be started.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated topic content, or null if failed.</returns>
    Task<string?> StartTopicAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Responds to a mention of the bot.
    /// </summary>
    /// <param name="guildId">The guild ID where the mention occurred.</param>
    /// <param name="mentionMessage">The message that mentioned the bot.</param>
    /// <param name="channel">Optional specific channel, otherwise responds in the same channel.</param>
    /// <returns>The generated response content, or null if failed.</returns>
    Task<string?> AnswerMentionAsync(ulong guildId, SocketMessage mentionMessage, IMessageChannel? channel = null);

    /// <summary>
    /// Asks a question to engage users in conversation.
    /// </summary>
    /// <param name="guildId">The guild ID where the question should be asked.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated question content, or null if failed.</returns>
    Task<string?> AskQuestionAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Shares interesting content to spark conversation.
    /// </summary>
    /// <param name="guildId">The guild ID where content should be shared.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated content, or null if failed.</returns>
    Task<string?> ShareInterestingContentAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Shares funny content or jokes.
    /// </summary>
    /// <param name="guildId">The guild ID where content should be shared.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated content, or null if failed.</returns>
    Task<string?> ShareFunnyContentAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Shares useful information or tips.
    /// </summary>
    /// <param name="guildId">The guild ID where content should be shared.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated content, or null if failed.</returns>
    Task<string?> ShareUsefulContentAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Shares news or current events.
    /// </summary>
    /// <param name="guildId">The guild ID where content should be shared.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated content, or null if failed.</returns>
    Task<string?> ShareNewsAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Increases engagement through various interaction methods.
    /// </summary>
    /// <param name="guildId">The guild ID where engagement should be increased.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated engagement content, or null if failed.</returns>
    Task<string?> IncreaseEngagementAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Gets the current engagement multiplier for a specific scope.
    /// Higher values indicate more engagement (e.g., after bot mentions).
    /// </summary>
    /// <param name="scopeId">The scope identifier to get the multiplier for.</param>
    /// <returns>The engagement multiplier (1.0 = normal, higher = more engaged).</returns>
    float GetEngagementMultiplier(ulong scopeId);

    /// <summary>
    /// Formats context messages in the same format as the /chat command: [username]: message
    /// </summary>
    /// <param name="scopeId">The scope identifier to format messages for.</param>
    /// <returns>Formatted message context suitable for AI processing.</returns>
    string FormatContextMessagesForAI(ulong scopeId);

    /// <summary>
    /// Sends an AI-generated message to a Discord channel based on recent conversation context.
    /// </summary>
    /// <param name="guild">The Discord guild where the message will be sent.</param>
    /// <param name="channel">The Discord channel to send the message to.</param>
    /// <returns>The generated message content if successful, null otherwise.</returns>
    Task<string?> SendContextAwareMessage(Discord.WebSocket.SocketGuild guild, Discord.IMessageChannel channel);
    #endregion
}
