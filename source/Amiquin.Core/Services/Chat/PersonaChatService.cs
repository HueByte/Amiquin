using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Chat.Providers;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly BotOptions _botOptions;

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
        IServiceProvider serviceProvider,
        IOptions<BotOptions> botOptions)
    {
        _logger = logger;
        _coreChatService = coreChatService;
        _messageCache = messageCache;
        _serverMetaService = serverMetaService;
        _serviceProvider = serviceProvider;
        _botOptions = botOptions.Value;
    }

    public async Task<string> ChatAsync(ulong instanceId, ulong userId, ulong botId, string message)
    {
        // Get or create semaphore for this instance
        var semaphore = _instanceSemaphores.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));

        // Check if there's already a pending request for this instance
        if (_pendingRequests.TryGetValue(instanceId, out var existingRequest))
        {
            var timeSinceStart = DateTime.UtcNow - existingRequest.StartTime;

            // If request is very recent (< 2 seconds), wait for it to complete
            if (timeSinceStart.TotalSeconds < 2 && existingRequest.CompletionSource != null)
            {
                _logger.LogInformation("Duplicate request detected for instance {InstanceId}, waiting for existing request", instanceId);
                try
                {
                    // Wait for the existing request to complete (with timeout)
                    var waitTask = existingRequest.CompletionSource.Task;
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                    var completedTask = await Task.WhenAny(waitTask, timeoutTask);

                    if (completedTask == waitTask)
                    {
                        return await waitTask;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error waiting for existing request for instance {InstanceId}", instanceId);
                }
            }
            else if (timeSinceStart.TotalSeconds < 10)
            {
                // If request is still recent, return a polite message
                _logger.LogInformation("Recent request still processing for instance {InstanceId}, providing feedback", instanceId);
                return "I'm still processing the previous request. Please wait a moment...";
            }
        }

        // Try to acquire the semaphore (non-blocking)
        if (!await semaphore.WaitAsync(100))
        {
            _logger.LogInformation("Instance {InstanceId} busy, request from user {UserId} queued", instanceId, userId);
            return "I'm currently processing another request for this server. Please try again in a moment.";
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

            // 1. Get message history from cache/database
            var messages = await GetMessageHistoryAsync(instanceId);

            // 2. Get server-specific persona and session context
            var (serverPersona, sessionContext) = await GetServerContextAsync(instanceId);

            // 3. Add the new user message to history
            var userMessage = new SessionMessage
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = message,
                CreatedAt = DateTime.UtcNow,
                IncludeInContext = true
            };
            messages.Add(userMessage);

            // 4. Select provider based on server configuration
            var provider = await GetServerProviderAsync(instanceId);
            _logger.LogInformation("Using provider {Provider} for instance {InstanceId}", provider ?? "default", instanceId);

            // 5. Send to LLM with fallback support
            var response = await _coreChatService.ChatAsync(
                instanceId,
                messages,
                customPersona: serverPersona,
                sessionContext: sessionContext,
                provider: provider);

            // 6. Store the exchange
            await StoreMessageExchangeAsync(instanceId, userId, botId, message, response.Content);

            // 7. Log comprehensive token usage
            LogTokenUsage(instanceId, response, provider);

            // 8. Check if optimization is needed
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

    private async Task<string?> GetServerProviderAsync(ulong instanceId)
    {
        // Get server-specific provider preference
        var serverMeta = await _serverMetaService.GetServerMetaAsync(instanceId);
        return serverMeta?.PreferredProvider;
    }

    private async Task StoreMessageExchangeAsync(
        ulong instanceId,
        ulong userId,
        ulong botId,
        string userMessage,
        string assistantMessage)
    {
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

            // Log detailed token usage
            _logger.LogInformation("Token Usage - Instance: {InstanceId}, Provider: {Provider}, Model: {Model}, " +
                                 "Prompt: {PromptTokens}, Completion: {CompletionTokens}, Total: {TotalTokens}",
                instanceId, providerName, model, promptTokens, completionTokens, totalTokens);

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
}