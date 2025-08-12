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
    /// Retrieves the context messages for a specific scope without clearing them.
    /// This is typically called when the bot needs to process accumulated messages for AI interaction.
    /// </summary>
    /// <param name="scopeId">The scope identifier to retrieve messages for.</param>
    /// <returns>An array of message content strings from the context.</returns>
    string[] GetContextMessages(ulong scopeId);

    /// <summary>
    /// Clears the context messages for a scope after successful engagement.
    /// This should be called only when the bot actually engages, not during the decision process.
    /// </summary>
    /// <param name="scopeId">The scope identifier to clear messages for.</param>
    void ClearContextMessages(ulong scopeId);

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
    /// Shares an opinion or perspective on topics being discussed.
    /// </summary>
    /// <param name="guildId">The guild ID where opinion should be shared.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated opinion content, or null if failed.</returns>
    Task<string?> ShareOpinionAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Lets the LLM decide how to respond based on the current message content and context.
    /// This gives the AI full autonomy to choose the most appropriate response type.
    /// </summary>
    /// <param name="guildId">The guild ID where the adaptive response should be sent.</param>
    /// <param name="channel">Optional specific channel, otherwise uses configured primary channel.</param>
    /// <returns>The generated adaptive content, or null if failed.</returns>
    Task<string?> AdaptiveResponseAsync(ulong guildId, IMessageChannel? channel = null);

    /// <summary>
    /// Gets the current engagement multiplier for a specific scope.
    /// Higher values indicate more engagement (e.g., after bot mentions).
    /// </summary>
    /// <param name="scopeId">The scope identifier to get the multiplier for.</param>
    /// <returns>The engagement multiplier (1.0 = normal, higher = more engaged).</returns>
    float GetEngagementMultiplier(ulong scopeId);

    /// <summary>
    /// Gets the current real-time activity level based on recent message timestamps.
    /// </summary>
    /// <param name="scopeId">The scope identifier to get the activity level for.</param>
    /// <returns>The activity level (0.1 = very low, 2.0 = extremely high).</returns>
    double GetCurrentActivityLevel(ulong scopeId);

    /// <summary>
    /// Formats context messages with user metadata: [username:userId] [message content]
    /// This format enables user mentions via &lt;@userId&gt; syntax in AI responses
    /// </summary>
    /// <param name="scopeId">The scope identifier to format messages for.</param>
    /// <returns>Formatted message context suitable for AI processing with user metadata.</returns>
    string FormatContextMessagesForAI(ulong scopeId);

    /// <summary>
    /// Sends an AI-generated message to a Discord channel based on recent conversation context.
    /// </summary>
    /// <param name="guild">The Discord guild where the message will be sent.</param>
    /// <param name="channel">The Discord channel to send the message to.</param>
    /// <returns>The generated message content if successful, null otherwise.</returns>
    Task<string?> SendContextAwareMessage(Discord.WebSocket.SocketGuild guild, Discord.IMessageChannel channel);

    /// <summary>
    /// Initializes the activity context by loading the last 10 messages from the general channel.
    /// This method is called during bot initialization to provide initial context for engagement.
    /// </summary>
    /// <param name="guild">The Discord guild to initialize context for.</param>
    /// <returns>A task representing the initialization operation.</returns>
    Task InitializeActivityContextAsync(Discord.WebSocket.SocketGuild guild);

    /// <summary>
    /// Resets the engagement multiplier for a specific scope back to baseline (1.0).
    /// Also clears any context messages to give the bot a fresh start.
    /// </summary>
    /// <param name="scopeId">The scope identifier to reset engagement for.</param>
    void ResetEngagement(ulong scopeId);
    #endregion
}