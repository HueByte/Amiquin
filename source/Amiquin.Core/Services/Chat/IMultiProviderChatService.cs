using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat.Providers;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Service interface for multi-provider chat operations.
/// Provides methods for chat completion using different AI providers.
/// </summary>
public interface IMultiProviderChatService
{
    /// <summary>
    /// Performs a chat completion using the configured provider.
    /// </summary>
    /// <param name="instanceId">The unique identifier for the chat instance.</param>
    /// <param name="messages">The list of messages representing the conversation history.</param>
    /// <param name="systemMessage">Optional system message to influence the AI's behavior.</param>
    /// <param name="provider">Optional provider override. If null, uses configured default.</param>
    /// <returns>The chat completion response from the AI model.</returns>
    Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId, 
        List<SessionMessage> messages, 
        string? systemMessage = null,
        string? provider = null);
    
    /// <summary>
    /// Exchanges a message with the AI model and returns the response.
    /// </summary>
    /// <param name="message">The message to send to the AI model.</param>
    /// <param name="systemMessage">Optional system message for context.</param>
    /// <param name="tokenLimit">The maximum number of tokens for the response.</param>
    /// <param name="provider">Optional provider override.</param>
    /// <returns>The AI model's response.</returns>
    Task<ChatCompletionResponse> ExchangeMessageAsync(
        string message, 
        string? systemMessage = null, 
        int tokenLimit = 1200,
        string? provider = null);
    
    /// <summary>
    /// Gets the current default provider name.
    /// </summary>
    string GetCurrentProvider();
    
    /// <summary>
    /// Gets all available providers.
    /// </summary>
    IEnumerable<string> GetAvailableProviders();
    
    /// <summary>
    /// Checks if a specific provider is available.
    /// </summary>
    Task<bool> IsProviderAvailableAsync(string providerName);
}