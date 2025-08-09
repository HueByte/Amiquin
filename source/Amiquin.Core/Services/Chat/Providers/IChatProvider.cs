using Amiquin.Core.Models;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Defines the contract for AI chat providers (OpenAI, Gemini, Grok, etc.)
/// </summary>
public interface IChatProvider
{
    /// <summary>
    /// The name of the provider (e.g., "OpenAI", "Gemini", "Grok")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Performs a chat completion using the provider's AI model
    /// </summary>
    /// <param name="messages">The conversation history</param>
    /// <param name="options">Options for the chat completion</param>
    /// <returns>The AI model's response</returns>
    Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<SessionMessage> messages, 
        ChatCompletionOptions options);
    
    /// <summary>
    /// Validates if the provider is properly configured and available
    /// </summary>
    Task<bool> IsAvailableAsync();
    
    /// <summary>
    /// Gets the maximum context length supported by the provider
    /// </summary>
    int MaxContextTokens { get; }
}