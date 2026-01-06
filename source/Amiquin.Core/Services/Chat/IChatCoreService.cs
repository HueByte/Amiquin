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

    /// <summary>
    /// Chat request - calls LLM with conversation history, session context, and cache optimization
    /// </summary>
    /// <param name="instanceId">Instance/channel ID for semaphore management</param>
    /// <param name="messages">List of conversation messages</param>
    /// <param name="customPersona">Optional custom persona to append to base persona</param>
    /// <param name="sessionContext">Optional session context (conversation summary)</param>
    /// <param name="sessionId">Optional session ID for prompt cache optimization (used by Grok x-grok-conv-id header)</param>
    /// <param name="provider">Optional provider override</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? sessionId = null,
        string? provider = null);

    /// <summary>
    /// Chat request - calls LLM with conversation history, session context, cache optimization, and specific model
    /// </summary>
    /// <param name="instanceId">Instance/channel ID for semaphore management</param>
    /// <param name="messages">List of conversation messages</param>
    /// <param name="customPersona">Optional custom persona to append to base persona</param>
    /// <param name="sessionContext">Optional session context (conversation summary)</param>
    /// <param name="sessionId">Optional session ID for prompt cache optimization (used by Grok x-grok-conv-id header)</param>
    /// <param name="provider">Optional provider override</param>
    /// <param name="model">Optional specific model to use (overrides provider's default model)</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> ChatAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? sessionId = null,
        string? provider = null,
        string? model = null);

    /// <summary>
    /// Chat request with cache-optimized message ordering.
    /// Memory context is appended at the end (not in system message) to maximize prompt cache hits.
    /// OpenAI and Grok cache from the left side of prompts, so stable content should come first.
    /// </summary>
    /// <param name="instanceId">Instance/channel ID for semaphore management</param>
    /// <param name="messages">List of conversation messages</param>
    /// <param name="customPersona">Optional custom persona to append to base persona (stable, cached)</param>
    /// <param name="sessionContext">Optional session context/summary (semi-stable, cached)</param>
    /// <param name="memoryContext">Optional memory context to append after conversation (dynamic, not cached)</param>
    /// <param name="sessionId">Optional session ID for prompt cache optimization</param>
    /// <param name="provider">Optional provider override</param>
    /// <param name="model">Optional specific model to use</param>
    /// <returns>The LLM response</returns>
    Task<ChatCompletionResponse> ChatWithMemoryContextAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? customPersona = null,
        string? sessionContext = null,
        string? memoryContext = null,
        string? sessionId = null,
        string? provider = null,
        string? model = null);
}