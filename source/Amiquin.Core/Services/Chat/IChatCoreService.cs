using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Service interface for core chat operations using AI models.
/// Provides methods for chat completion and message exchange functionality.
/// </summary>
public interface IChatCoreService
{
    /// <summary>
    /// Performs a chat completion using the specified message history and optional persona.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the chat instance.</param>
    /// <param name="messageHistory">The list of chat messages representing the conversation history.</param>
    /// <param name="personaMessage">Optional persona message to influence the AI's behavior.</param>
    /// <returns>The chat completion response from the AI model.</returns>
    Task<ChatCompletion> ChatAsync(ulong instanceId, List<ChatMessage> messageHistory, ChatMessage? personaMessage = null);

    /// <summary>
    /// Exchanges a message with the AI model and returns the response.
    /// </summary>
    /// <param name="message">The message to send to the AI model.</param>
    /// <param name="developerPersonaChatMessage">Optional developer persona message for context.</param>
    /// <param name="tokenLimit">The maximum number of tokens for the response. Default is 1200.</param>
    /// <returns>The AI model's response as a string.</returns>
    Task<string> ExchangeMessageAsync(string message, ChatMessage? developerPersonaChatMessage = null, int tokenLimit = 1200);
}