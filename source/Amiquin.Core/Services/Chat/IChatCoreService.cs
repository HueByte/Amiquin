using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat.Providers;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Service interface for core chat operations using AI models.
/// Provides methods for chat completion and message exchange functionality.
/// </summary>
public interface IChatCoreService
{
    /// <summary>
    /// Core request - sends only system message + prompt to LLM without conversation history
    /// </summary>
    /// <param name="prompt">The prompt/message to send</param>
    /// <param name="customPersona">Optional custom persona to append to base persona</param>
    /// <param name="tokenLimit">Maximum tokens for response</param>
    /// <param name="provider">Optional provider override</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> CoreRequestAsync(
        string prompt,
        string? customPersona = null,
        int tokenLimit = 1200,
        string? provider = null);

    /// <summary>
    /// Chat request - calls LLM with conversation history
    /// </summary>
    /// <param name="instanceId">Instance/channel ID for semaphore management</param>
    /// <param name="messages">List of conversation messages</param>
    /// <param name="customPersona">Optional custom persona to append to base persona</param>
    /// <param name="provider">Optional provider override</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? provider = null);

    /// <summary>
    /// Chat request - calls LLM with conversation history and session context
    /// </summary>
    /// <param name="instanceId">Instance/channel ID for semaphore management</param>
    /// <param name="messages">List of conversation messages</param>
    /// <param name="customPersona">Optional custom persona to append to base persona</param>
    /// <param name="sessionContext">Optional session context (conversation summary)</param>
    /// <param name="provider">Optional provider override</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? provider = null);
}