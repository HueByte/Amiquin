using Amiquin.Core.Configuration;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.Memory;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.SessionManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Persona chat service that handles Discord message flow
/// </summary>
public class PersonaChatService : IPersonaChatService
{
    private readonly ILogger<PersonaChatService> _logger;
    private readonly IChatCoreService _coreChatService;
    private readonly IMessageCacheService _messageCache;
    private readonly IServerMetaService _serverMetaService;
    private readonly IMemoryService _memoryService;
    private readonly ISessionManagerService _sessionManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotOptions _botOptions;
    private readonly MemoryOptions _memoryOptions;

    // Semaphores to prevent duplicate requests per instance
    private static readonly ConcurrentDictionary<ulong, SemaphoreSlim> _instanceSemaphores = new();

    // Track pending requests to provide better user feedback
    private static readonly ConcurrentDictionary<ulong, PendingRequestInfo> _pendingRequests = new();

    private class PendingRequestInfo
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public string UserMessage { get; set; } = string.Empty;
        public ulong UserId { get; set; }
        public TaskCompletionSource<string>? CompletionSource { get; set; }
    }

    public PersonaChatService(
        ILogger<PersonaChatService> logger,
        IChatCoreService coreChatService,
        IMessageCacheService messageCache,
        IServerMetaService serverMetaService,
        IMemoryService memoryService,
        ISessionManagerService sessionManager,
        IServiceProvider serviceProvider,
        IOptions<BotOptions> botOptions,
        IOptions<MemoryOptions> memoryOptions)
    {
        _logger = logger;
        _coreChatService = coreChatService;
        _messageCache = messageCache;
        _serverMetaService = serverMetaService;
        _memoryService = memoryService;
        _sessionManager = sessionManager;
        _serviceProvider = serviceProvider;
        _botOptions = botOptions.Value;
        _memoryOptions = memoryOptions.Value;
    }

    public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
    {
        // Get or create semaphore for this instance
        var semaphore = _instanceSemaphores.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));

        // Check if there's already a pending request for this instance
        if (_pendingRequests.TryGetValue(instanceId, out var existingRequest))
        {
            var timeSinceStart = DateTime.UtcNow - existingRequest.StartTime;

            // If request is very recent (< 5 seconds), silently skip this duplicate
            if (timeSinceStart.TotalSeconds < 5)
            {
                _logger.LogDebug("Duplicate request detected for instance {InstanceId}, silently skipping", instanceId);
                return string.Empty; // Return empty string to indicate silent skip
            }
        }

        // Try to acquire the semaphore (non-blocking) - if busy, silently skip
        if (!await semaphore.WaitAsync(0)) // No timeout - immediate check only
        {
            _logger.LogDebug("Instance {InstanceId} busy, silently skipping request from user {UserId}", instanceId, userId);
            return string.Empty; // Return empty string to indicate silent skip
        }

        var completionSource = new TaskCompletionSource<string>();
        var pendingInfo = new PendingRequestInfo
        {
            StartTime = DateTime.UtcNow,
            UserMessage = message,
            UserId = userId,
            CompletionSource = completionSource
        };

        _pendingRequests[instanceId] = pendingInfo;

        try
        {
            _logger.LogDebug("Processing chat for instance {InstanceId}, user {UserId}", instanceId, userId);

            // 0. Check if session needs refresh (stale after inactivity) or compaction
            var sessionRefreshResult = await CheckAndRefreshSessionAsync(instanceId, userId);
            string? refreshMemoryContext = sessionRefreshResult?.MemoryContext;

            // 1. Get message history from cache/database
            var messages = await GetMessageHistoryAsync(instanceId);

            // 1.5. Check if compaction is needed
            await CheckAndCompactSessionAsync(instanceId);

            // 2. Get server-specific persona and session context
            var (serverPersona, sessionContext) = await GetServerContextAsync(instanceId);

            // 3. Get relevant memories for context (now with cross-session support)
            var sessionId = instanceId.ToString();
            var memoryContext = refreshMemoryContext ?? await _memoryService.GetCombinedMemoryContextAsync(
                sessionId,
                userId,
                instanceId, // ServerId is the same as instanceId for server-based chat
                message);

            // 4. Add the new user message to history
            var userMessage = new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = message,
                CreatedAt = DateTime.UtcNow,
                IncludeInContext = true
            };
            messages.Add(userMessage);

            // 5. Select provider and model based on server/session configuration
            var (provider, model) = await GetServerProviderAndModelAsync(instanceId);
            _logger.LogInformation("Using provider {Provider} with model {Model} for instance {InstanceId}",
                provider ?? "default", model ?? "default", instanceId);

            // 6. Combine session context with memory context
            var combinedContext = CombineContexts(sessionContext, memoryContext);

            // 7. Send to LLM with fallback support
            // Pass session ID for prompt cache optimization (Grok uses x-grok-conv-id header)
            var response = await _coreChatService.ChatAsync(
                instanceId,
                messages,
                customPersona: serverPersona,
                sessionContext: combinedContext,
                sessionId: sessionId,
                provider: provider,
                model: model);

            // 8. Store the exchange
            await StoreMessageExchangeAsync(instanceId, userId, botId, message, response.Content);

            // 9. Extract and store memories from the conversation
            var assistantMessage = new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "assistant",
                Content = response.Content,
                CreatedAt = DateTime.UtcNow,
                IncludeInContext = true
            };
            var conversationMessages = messages.Skip(Math.Max(0, messages.Count - 6)).ToList(); // Last 6 messages for context
            conversationMessages.Add(assistantMessage);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await _memoryService.ExtractMemoriesFromConversationAsync(sessionId, conversationMessages);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract memories for session {SessionId}", sessionId);
                }
            });

            // 10. Log comprehensive token usage
            LogTokenUsage(instanceId, response, provider);

            // 11. Check if optimization is needed
            if (response.TotalTokens.HasValue && response.TotalTokens > _botOptions.MaxTokens * 0.4)
            {
                _logger.LogInformation("Token limit approaching for instance {InstanceId}, triggering optimization", instanceId);
                await OptimizeHistoryAsync(instanceId, messages, sessionContext);
            }

            _logger.LogInformation("Chat completed for instance {InstanceId}", instanceId);

            // Signal completion to any waiting requests
            completionSource.SetResult(response.Content);

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat for instance {InstanceId}", instanceId);
            var errorMessage = "I'm sorry, I encountered an error processing your message. Please try again.";

            // Signal completion with error
            completionSource.SetResult(errorMessage);

            return errorMessage;
        }
        finally
        {
            // Clean up pending request tracking
            _pendingRequests.TryRemove(instanceId, out _);

            // Release the semaphore
            semaphore.Release();
        }
    }

    public async Task<string> ExchangeMessageAsync(ulong instanceId, string message)
    {
        try
        {
            // Get server persona for one-off exchanges
            var serverMeta = await _serverMetaService.GetServerMetaAsync(instanceId);
            var serverPersona = serverMeta?.Persona;

            // Use core request for stateless exchange
            var response = await _coreChatService.CoreRequestAsync(
                message,
                customPersona: serverPersona,
                tokenLimit: 1200);

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message exchange for instance {InstanceId}", instanceId);
            return "I'm sorry, I encountered an error processing your message.";
        }
    }

    private async Task<List<SessionMessage>> GetMessageHistoryAsync(ulong instanceId)
    {
        // Try to get from cache first
        var cachedMessages = await _messageCache.GetOrCreateChatMessagesAsync(instanceId);

        if (cachedMessages != null && cachedMessages.Any())
        {
            // Convert ChatMessage to SessionMessage
            return cachedMessages.Select(m => new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = GetRoleFromChatMessage(m),
                Content = m.Content.FirstOrDefault()?.Text ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                IncludeInContext = true
            }).ToList();
        }

        // If no cache, get from database
        var chatSessionService = _serviceProvider.GetRequiredService<IChatSessionService>();
        var session = await chatSessionService.GetOrCreateServerSessionAsync(instanceId);

        return session.Messages
            .Where(m => m.IncludeInContext)
            .OrderBy(m => m.CreatedAt)
            .ToList();
    }

    private async Task<(string? serverPersona, string? sessionContext)> GetServerContextAsync(ulong instanceId)
    {
        // Get server-specific persona
        var serverMeta = await _serverMetaService.GetServerMetaAsync(instanceId);
        var serverPersona = serverMeta?.Persona;

        // Get session context (conversation summary)
        var chatSessionService = _serviceProvider.GetRequiredService<IChatSessionService>();
        var session = await chatSessionService.GetOrCreateServerSessionAsync(instanceId);

        return (serverPersona, session.Context);
    }

    private string? CombineContexts(string? sessionContext, string? memoryContext)
    {
        var contexts = new List<string>();

        if (!string.IsNullOrWhiteSpace(sessionContext))
        {
            contexts.Add($"Previous conversation summary:\n{sessionContext}");
        }

        if (!string.IsNullOrWhiteSpace(memoryContext))
        {
            contexts.Add($"Relevant memories:\n{memoryContext}");
        }

        return contexts.Any() ? string.Join("\n\n", contexts) : null;
    }

    private async Task<(string? Provider, string? Model)> GetServerProviderAndModelAsync(ulong instanceId)
    {
        // Get server-specific provider and model preference from server meta
        var serverMeta = await _serverMetaService.GetServerMetaAsync(instanceId);
        var provider = serverMeta?.PreferredProvider;

        // Try to get model from the active session first
        var activeSession = await _sessionManager.GetActiveSessionAsync(instanceId);
        var model = activeSession?.Model;

        // Fallback to ServerMeta.PreferredModel if session doesn't have a model
        if (string.IsNullOrWhiteSpace(model))
        {
            model = serverMeta?.PreferredModel;
            if (!string.IsNullOrWhiteSpace(model))
            {
                _logger.LogDebug("Using server preferred model: {Model} for instance {InstanceId}", model, instanceId);
            }
        }
        else
        {
            _logger.LogDebug("Using session model: {Model} for instance {InstanceId}", model, instanceId);
        }

        return (provider, model);
    }

    private async Task StoreMessageExchangeAsync(
        ulong instanceId,
        ulong userId,
        ulong botId,
        string userMessage,
        string assistantMessage)
    {
        // Ensure server metadata exists before storing messages
        // This prevents foreign key constraint violations
        var serverMeta = await _serverMetaService.GetServerMetaAsync(instanceId);
        if (serverMeta == null)
        {
            _logger.LogWarning("ServerMeta not found for serverId {ServerId}, creating default metadata", instanceId);

            // Create server metadata with a fallback name
            await _serverMetaService.CreateServerMetaAsync(instanceId, $"Server_{instanceId}");
        }

        // Create ChatMessage objects
        var userChatMessage = OpenAI.Chat.ChatMessage.CreateUserMessage(userMessage);
        var assistantChatMessage = OpenAI.Chat.ChatMessage.CreateAssistantMessage(assistantMessage);

        // Create Message models for database
        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = instanceId,
                Content = userMessage,
                IsUser = true,
                AuthorId = userId,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid().ToString(),
                ServerId = instanceId,
                Content = assistantMessage,
                IsUser = false,
                AuthorId = botId,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Store in cache and database (MessageCacheService handles both)
        await _messageCache.AddChatExchangeAsync(
            instanceId,
            new List<OpenAI.Chat.ChatMessage> { userChatMessage, assistantChatMessage },
            messages);
    }

    /// <inheritdoc/>
    public async Task<(bool success, string message)> TriggerHistoryOptimizationAsync(ulong instanceId)
    {
        try
        {
            _logger.LogInformation("Manual history optimization triggered for instance {InstanceId}", instanceId);

            // Get current messages from message history
            var messages = await GetMessageHistoryAsync(instanceId);

            if (messages == null || messages.Count < 5)
            {
                return (false, "Not enough messages to optimize (minimum 5 required)");
            }

            // Get server context (persona and session context)
            var (serverPersona, sessionContext) = await GetServerContextAsync(instanceId);

            // Get original count before optimization
            var originalCount = messages.Count;

            // Trigger the optimization
            await OptimizeHistoryAsync(instanceId, messages, sessionContext);

            // Get remaining message count from cache
            var remainingMessages = _messageCache.GetChatMessageCount(instanceId);
            var optimizedCount = Math.Max(0, originalCount - remainingMessages);

            var resultMessage = $"Successfully optimized history. Compacted {optimizedCount} messages into summary context. {remainingMessages} recent messages retained.";
            _logger.LogInformation("History optimization completed for instance {InstanceId}: {Result}", instanceId, resultMessage);

            return (true, resultMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manually trigger history optimization for instance {InstanceId}", instanceId);
            return (false, $"Optimization failed: {ex.Message}");
        }
    }

    private async Task OptimizeHistoryAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? existingContext)
    {
        try
        {
            var messagesToKeep = 10; // Keep last 10 exchanges
            var messagesToSummarize = messages.Take(Math.Max(0, messages.Count - messagesToKeep)).ToList();

            _logger.LogInformation("Starting history optimization for instance {InstanceId} - Total messages: {TotalMessages}, Messages to summarize: {MessagesToSummarize}, Messages to keep: {MessagesToKeep}",
                instanceId, messages.Count, messagesToSummarize.Count, messagesToKeep);

            if (!messagesToSummarize.Any())
            {
                _logger.LogDebug("No messages to summarize for instance {InstanceId}", instanceId);
                return;
            }

            // Create summary prompt
            var conversationText = string.Join("\n",
                messagesToSummarize.Select(m => $"[{m.Role}]: {m.Content}"));

            var summaryPrompt = "Summarize this conversation history concisely, preserving key context and topics (max 400 tokens):\n\n" + conversationText;

            // Get summary using core request
            _logger.LogDebug("Requesting summary for instance {InstanceId} with {CharacterCount} characters of conversation history",
                instanceId, conversationText.Length);

            var summaryResponse = await _coreChatService.CoreRequestAsync(
                summaryPrompt,
                tokenLimit: 400);

            _logger.LogInformation("Generated summary for instance {InstanceId} - Summary length: {SummaryLength} characters, Tokens used: {TokensUsed}",
                instanceId, summaryResponse.Content.Length, summaryResponse.TotalTokens ?? 0);

            // Update session context
            var newContext = string.IsNullOrWhiteSpace(existingContext)
                ? summaryResponse.Content
                : $"{existingContext}\n\n{summaryResponse.Content}";

            // Check if context needs self-summarization
            if (newContext.Length > 2000) // Rough check
            {
                _logger.LogInformation("Context too long for instance {InstanceId} ({ContextLength} chars), consolidating summaries",
                    instanceId, newContext.Length);

                var consolidatePrompt = $"Consolidate these conversation summaries into one concise summary (max 400 tokens):\n\n{newContext}";
                var consolidatedResponse = await _coreChatService.CoreRequestAsync(
                    consolidatePrompt,
                    tokenLimit: 400);

                _logger.LogInformation("Consolidated context for instance {InstanceId} - Old length: {OldLength}, New length: {NewLength}, Tokens used: {TokensUsed}",
                    instanceId, newContext.Length, consolidatedResponse.Content.Length, consolidatedResponse.TotalTokens ?? 0);

                _logger.LogInformation("Final context for instance {InstanceId} - Length: {Length} characters",
                    instanceId, consolidatedResponse.Content.Length);

                newContext = consolidatedResponse.Content;
            }

            // Save updated context
            var chatSessionRepository = _serviceProvider.GetRequiredService<IChatSessionRepository>();
            var chatSessionService = _serviceProvider.GetRequiredService<IChatSessionService>();
            var session = await chatSessionService.GetOrCreateServerSessionAsync(instanceId);

            await chatSessionRepository.UpdateSessionContextAsync(
                session.Id,
                newContext,
                newContext.Length / 4); // Rough token estimate

            // Clear old messages from cache
            _messageCache.ClearOldMessages(instanceId, messagesToKeep);

            // Trigger memory extraction from the conversation being summarized
            if (messagesToSummarize.Any())
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var sessionIdStr = instanceId.ToString();
                        await _memoryService.ExtractMemoriesFromConversationAsync(sessionIdStr, messagesToSummarize);
                        _logger.LogDebug("Extracted memories from {Count} messages during optimization for instance {InstanceId}", 
                            messagesToSummarize.Count, instanceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract memories during optimization for instance {InstanceId}", instanceId);
                    }
                });
            }

            _logger.LogInformation("Optimized history for instance {InstanceId}, summarized {Count} messages",
                instanceId, messagesToSummarize.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize history for instance {InstanceId}", instanceId);
        }
    }

    private string GetRoleFromChatMessage(OpenAI.Chat.ChatMessage message)
    {
        // Parse the message to determine role
        // Since ChatMessage doesn't expose Role directly, we need to infer it
        // from the message content or type
        var content = message.Content?.FirstOrDefault()?.Text ?? "";

        // Check if it's a system message (usually starts with specific patterns)
        if (message.Content?.Any() == true)
        {
            var firstContent = message.Content.First();
            var kind = firstContent.Kind.ToString();

            // Try to determine from Kind or other properties
            // Default to "user" if uncertain
            return "user";
        }

        return "user";
    }

    /// <summary>
    /// Logs comprehensive token usage statistics for monitoring and cost tracking
    /// </summary>
    private void LogTokenUsage(ulong instanceId, ChatCompletionResponse response, string? provider)
    {
        try
        {
            var providerName = provider ?? "unknown";
            var model = response.Model ?? "unknown";

            // Basic token information
            var promptTokens = response.PromptTokens ?? 0;
            var completionTokens = response.CompletionTokens ?? 0;
            var totalTokens = response.TotalTokens ?? (promptTokens + completionTokens);
            var cachedTokens = response.CachedPromptTokens ?? 0;
            var cacheHitRatio = response.CacheHitRatio ?? 0f;

            // Log detailed token usage including cache information
            if (cachedTokens > 0)
            {
                _logger.LogInformation(
                    "Token Usage - Instance: {InstanceId}, Provider: {Provider}, Model: {Model}, " +
                    "Prompt: {PromptTokens} (Cached: {CachedTokens}, {CacheHitRatio:P0}), " +
                    "Completion: {CompletionTokens}, Total: {TotalTokens}",
                    instanceId, providerName, model, promptTokens, cachedTokens, cacheHitRatio,
                    completionTokens, totalTokens);
            }
            else
            {
                _logger.LogInformation(
                    "Token Usage - Instance: {InstanceId}, Provider: {Provider}, Model: {Model}, " +
                    "Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                    instanceId, providerName, model, promptTokens, completionTokens, totalTokens);
            }

            // Log additional metadata if available
            if (response.Metadata?.Count > 0)
            {
                var metadataLog = new StringBuilder();
                metadataLog.Append("Token Metadata - ");

                foreach (var kvp in response.Metadata)
                {
                    // Look for cached tokens or other relevant metadata
                    if (kvp.Key.ToLowerInvariant().Contains("cached") ||
                        kvp.Key.ToLowerInvariant().Contains("token") ||
                        kvp.Key.ToLowerInvariant().Contains("cost"))
                    {
                        metadataLog.Append($"{kvp.Key}: {kvp.Value}, ");
                    }
                }

                var metadataString = metadataLog.ToString().TrimEnd(' ', ',');
                if (metadataString.Length > "Token Metadata - ".Length)
                {
                    _logger.LogInformation(metadataString);
                }
            }

            // Warning for high token usage
            if (totalTokens > 3000)
            {
                _logger.LogWarning("High token usage detected - Instance: {InstanceId}, Tokens: {TotalTokens}, " +
                                 "Consider optimizing context or implementing earlier summarization",
                    instanceId, totalTokens);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log token usage for instance {InstanceId}", instanceId);
        }
    }

    /// <summary>
    /// Checks if the session is stale and refreshes it if needed
    /// </summary>
    private async Task<SessionRefreshResult?> CheckAndRefreshSessionAsync(ulong instanceId, ulong userId)
    {
        if (!_memoryOptions.Session.EnableAutoRefresh)
            return null;

        try
        {
            var refreshResult = await _sessionManager.RefreshStaleSessionAsync(instanceId, userId);

            if (refreshResult.WasRefreshed)
            {
                _logger.LogInformation(
                    "Session refreshed for instance {InstanceId}. Inactive for {Minutes} minutes. Retrieved {MemoryCount} memories for context",
                    instanceId,
                    (int)refreshResult.InactivityDuration.TotalMinutes,
                    refreshResult.MemoriesRetrieved);
            }

            return refreshResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check/refresh session for instance {InstanceId}", instanceId);
            return null;
        }
    }

    /// <summary>
    /// Checks if the session needs compaction and performs it if needed
    /// </summary>
    private async Task CheckAndCompactSessionAsync(ulong instanceId)
    {
        try
        {
            var activeSession = await _sessionManager.GetActiveSessionAsync(instanceId);
            if (activeSession == null)
                return;

            var needsCompaction = await _sessionManager.NeedsCompactionAsync(
                activeSession.Id,
                _memoryOptions.Session.MaxMessagesBeforeCompaction);

            if (needsCompaction)
            {
                _logger.LogInformation("Session {SessionId} needs compaction, starting...", activeSession.Id);

                var compactionResult = await _sessionManager.CompactSessionAsync(
                    activeSession.Id,
                    _memoryOptions.Session.MessagesToKeepAfterCompaction);

                if (compactionResult.WasCompacted)
                {
                    _logger.LogInformation(
                        "Session {SessionId} compacted: archived {Archived} messages, kept {Kept}, created {Memories} memories",
                        activeSession.Id,
                        compactionResult.MessagesArchived,
                        compactionResult.MessagesKept,
                        compactionResult.MemoriesCreated);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check/compact session for instance {InstanceId}", instanceId);
        }
    }
}