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
using Amiquin.Core.Services.WebSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

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
    private readonly IWebSearchService? _webSearchService;
    private readonly BotOptions _botOptions;
    private readonly MemoryOptions _memoryOptions;
    private readonly ChatOptions _chatOptions;

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
        IOptions<MemoryOptions> memoryOptions,
        IOptions<ChatOptions> chatOptions)
    {
        _logger = logger;
        _coreChatService = coreChatService;
        _messageCache = messageCache;
        _serverMetaService = serverMetaService;
        _memoryService = memoryService;
        _sessionManager = sessionManager;
        _serviceProvider = serviceProvider;
        _webSearchService = serviceProvider.GetService<IWebSearchService>();
        _botOptions = botOptions.Value;
        _memoryOptions = memoryOptions.Value;
        _chatOptions = chatOptions.Value;
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

            // 6. Execute ReAct reasoning loop if enabled and message qualifies
            ChatCompletionResponse response;
            if (ShouldUseReActReasoning(message))
            {
                response = await ExecuteReActLoopAsync(
                    instanceId,
                    messages,
                    serverPersona,
                    sessionContext,
                    memoryContext,
                    sessionId,
                    provider,
                    model,
                    userId);
            }
            else
            {
                // 7. Send to LLM with cache-optimized message ordering
                // Memory context is APPENDED (not in system message) to maximize prompt cache hits
                // OpenAI/Grok cache from the left, so: system + persona + session context = cached prefix
                response = await _coreChatService.ChatWithMemoryContextAsync(
                    instanceId,
                    messages,
                    customPersona: serverPersona,
                    sessionContext: sessionContext,
                    memoryContext: memoryContext,
                    sessionId: sessionId,
                    provider: provider,
                    model: model);
            }

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

            // 11. Check if optimization is needed (triggered at configured threshold, default 80%)
            if (response.TotalTokens.HasValue && response.TotalTokens > _botOptions.ConversationTokenLimit * _botOptions.HistoryOptimizationThreshold)
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

    /// <summary>
    /// Determines if ReAct reasoning should be used for this message.
    /// </summary>
    private bool ShouldUseReActReasoning(string message)
    {
        if (!_chatOptions.ReAct.Enabled)
            return false;

        // Skip short messages (greetings, simple reactions)
        if (message.Length < _chatOptions.ReAct.MinMessageLengthForReasoning)
            return false;

        return true;
    }

    /// <summary>
    /// Executes the ReAct (Reason-Act-Think) loop for enhanced conversation handling.
    /// This is a lightweight but impressive reasoning process designed for conversation bots.
    /// </summary>
    private async Task<ChatCompletionResponse> ExecuteReActLoopAsync(
        ulong instanceId,
        List<SessionMessage> messages,
        string? serverPersona,
        string? sessionContext,
        string? memoryContext,
        string sessionId,
        string? provider,
        string? model,
        ulong userId)
    {
        var reactConfig = _chatOptions.ReAct;
        var reasoning = new ReActReasoning();
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        _logger.LogDebug("Starting ReAct loop for instance {InstanceId}, max iterations: {MaxIterations}",
            instanceId, reactConfig.MaxIterations);

        // ReAct Loop - enhanced version with multiple actions and self-reflection
        for (int iteration = 0; iteration < reactConfig.MaxIterations; iteration++)
        {
            // THINK: Analyze the situation
            var thoughtPrompt = BuildThoughtPrompt(lastUserMessage, memoryContext, iteration, reasoning, messages);

            var thoughtResponse = await _coreChatService.CoreRequestAsync(
                thoughtPrompt,
                customPersona: null, // No persona for reasoning
                tokenLimit: reactConfig.ReasoningTokenLimit,
                provider: provider);

            var thought = ParseThought(thoughtResponse.Content);
            reasoning.Thoughts.Add(thought);

            if (reactConfig.LogReasoningTrace)
            {
                _logger.LogDebug("ReAct [{Iteration}/{Max}] Action: {Action} | Confidence: {Confidence:P0} | Analysis: {Analysis}",
                    iteration + 1, reactConfig.MaxIterations, thought.Action, thought.Confidence, thought.Analysis);
            }

            // Check if we're confident enough to respond
            if (thought.Action == ReActAction.Respond && thought.Confidence >= reactConfig.ConfidenceThreshold)
            {
                reasoning.FinalAction = "respond_confident";
                reasoning.FinalConfidence = thought.Confidence;
                break;
            }

            // ACT: Execute the decided action
            switch (thought.Action)
            {
                case ReActAction.Respond:
                    // Low confidence respond - continue reasoning if we have iterations left
                    if (iteration < reactConfig.MaxIterations - 1 && thought.Confidence < reactConfig.ConfidenceThreshold)
                    {
                        reasoning.Observations.Add($"Low confidence ({thought.Confidence:P0}), continuing analysis...");
                        continue;
                    }
                    reasoning.FinalAction = "respond";
                    reasoning.FinalConfidence = thought.Confidence;
                    goto LoopComplete;

                case ReActAction.RecallMemory:
                    // Try to get more specific memories
                    if (reactConfig.UseMemoriesInReasoning && !string.IsNullOrEmpty(thought.ActionTarget))
                    {
                        var additionalMemories = await _memoryService.GetCombinedMemoryContextAsync(
                            sessionId, userId, instanceId, thought.ActionTarget);
                        if (!string.IsNullOrWhiteSpace(additionalMemories))
                        {
                            memoryContext = string.IsNullOrWhiteSpace(memoryContext)
                                ? additionalMemories
                                : $"{memoryContext}\n\n[Additional context for '{thought.ActionTarget}']\n{additionalMemories}";
                            reasoning.Observations.Add($"Found memories about: {thought.ActionTarget}");
                        }
                        else
                        {
                            reasoning.Observations.Add($"No memories found for: {thought.ActionTarget}");
                        }
                    }
                    break;

                case ReActAction.StoreMemory:
                    // Store important information to long-term memory
                    if (!string.IsNullOrEmpty(thought.ActionTarget))
                    {
                        try
                        {
                            // Determine memory type based on content
                            var memoryType = DetermineMemoryType(thought.ActionTarget);

                            // Store the memory with appropriate scope
                            var storedMemory = await _memoryService.CreateScopedMemoryAsync(
                                sessionId: sessionId,
                                content: thought.ActionTarget,
                                memoryType: memoryType,
                                userId: userId,
                                serverId: instanceId,
                                scope: MemoryScope.User, // Default to user scope for intentional memories
                                importance: 0.8f); // High importance for explicitly stored memories

                            reasoning.Observations.Add($"Stored to memory: {thought.ActionTarget.Substring(0, Math.Min(50, thought.ActionTarget.Length))}{(thought.ActionTarget.Length > 50 ? "..." : "")}");
                            _logger.LogInformation("Stored memory via ReAct for user {UserId}: {Content}", userId, thought.ActionTarget);
                        }
                        catch (Exception ex)
                        {
                            reasoning.Observations.Add($"Failed to store memory: {ex.Message}");
                            _logger.LogWarning(ex, "Failed to store memory via ReAct for user {UserId}", userId);
                        }
                    }
                    else
                    {
                        reasoning.Observations.Add("Cannot store memory: no content specified");
                    }
                    break;

                case ReActAction.AnalyzeContext:
                    // Deeper analysis of conversation context
                    var contextAnalysis = AnalyzeConversationContext(messages, thought.ActionTarget);
                    reasoning.Observations.Add($"Context analysis: {contextAnalysis}");
                    break;

                case ReActAction.ConsiderTone:
                    // Analyze the appropriate tone for response
                    var toneAnalysis = AnalyzeTone(lastUserMessage, messages);
                    reasoning.Observations.Add($"Tone consideration: {toneAnalysis}");
                    reasoning.SuggestedTone = toneAnalysis;
                    break;

                case ReActAction.Reflect:
                    // Self-reflection on reasoning so far
                    if (reactConfig.EnableSelfReflection && reasoning.Thoughts.Count > 1)
                    {
                        var reflection = await PerformSelfReflectionAsync(reasoning, lastUserMessage, provider);
                        reasoning.Observations.Add($"Reflection: {reflection}");
                    }
                    break;

                case ReActAction.Clarify:
                    // Note what needs clarification (we don't interrupt the conversation)
                    reasoning.Observations.Add($"Clarification needed: {thought.ActionTarget}");
                    reasoning.NeedsClarification = true;
                    reasoning.ClarificationTopic = thought.ActionTarget;
                    break;

                case ReActAction.WebSearch:
                    // Perform web search for real-time information
                    if (_webSearchService != null && !string.IsNullOrEmpty(thought.ActionTarget))
                    {
                        _logger.LogDebug("Performing web search for: {Query}", thought.ActionTarget);
                        var searchResult = await _webSearchService.SearchAsync(thought.ActionTarget, maxResults: 3);

                        if (searchResult.Success && searchResult.Items.Any())
                        {
                            var searchSummary = string.Join("\n", searchResult.Items.Select((item, idx) =>
                                $"{idx + 1}. {item.Title}: {item.Snippet}"));
                            reasoning.Observations.Add($"Web search results for '{thought.ActionTarget}':\n{searchSummary}");
                            _logger.LogInformation("Web search completed, found {Count} results", searchResult.Items.Count);
                        }
                        else
                        {
                            reasoning.Observations.Add($"Web search for '{thought.ActionTarget}' returned no results");
                            _logger.LogWarning("Web search failed or returned no results: {Error}", searchResult.ErrorMessage);
                        }
                    }
                    else if (_webSearchService == null)
                    {
                        reasoning.Observations.Add("Web search requested but service not available");
                        _logger.LogWarning("Web search requested but IWebSearchService is not configured");
                    }
                    break;

                default:
                    // Unknown action, proceed to respond
                    reasoning.FinalAction = "respond";
                    goto LoopComplete;
            }
        }

    LoopComplete:

        // If we exhausted iterations without deciding, default to respond
        if (string.IsNullOrEmpty(reasoning.FinalAction))
        {
            reasoning.FinalAction = "respond_exhausted";
            reasoning.FinalConfidence = reasoning.Thoughts.LastOrDefault()?.Confidence ?? 0.5f;
        }

        if (reactConfig.LogReasoningTrace)
        {
            _logger.LogInformation(
                "ReAct completed for instance {InstanceId} | Iterations: {ThoughtCount} | Final: {FinalAction} | Confidence: {Confidence:P0}",
                instanceId, reasoning.Thoughts.Count, reasoning.FinalAction, reasoning.FinalConfidence);
        }

        // Build enriched context from reasoning
        var enrichedMemoryContext = BuildEnrichedContext(memoryContext, reasoning);

        // Use cache-optimized method for final response
        return await _coreChatService.ChatWithMemoryContextAsync(
            instanceId,
            messages,
            customPersona: serverPersona,
            sessionContext: sessionContext,
            memoryContext: enrichedMemoryContext,
            sessionId: sessionId,
            provider: provider,
            model: model);
    }

    /// <summary>
    /// Builds the prompt for the ReAct thinking step.
    /// </summary>
    private string BuildThoughtPrompt(string userMessage, string? memoryContext, int iteration, ReActReasoning reasoning, List<SessionMessage> conversationHistory)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a reasoning engine analyzing a conversation. Think step-by-step.");
        sb.AppendLine();
        sb.AppendLine($"ITERATION: {iteration + 1}/{_chatOptions.ReAct.MaxIterations}");
        sb.AppendLine();
        sb.AppendLine($"USER MESSAGE: \"{userMessage}\"");

        // Add conversation context summary
        if (conversationHistory.Count > 1)
        {
            var recentExchanges = conversationHistory
                .TakeLast(Math.Min(6, conversationHistory.Count - 1))
                .Select(m => $"[{m.Role}]: {(m.Content.Length > 100 ? m.Content[..100] + "..." : m.Content)}");
            sb.AppendLine();
            sb.AppendLine("RECENT CONVERSATION:");
            foreach (var exchange in recentExchanges)
            {
                sb.AppendLine($"  {exchange}");
            }
        }

        if (!string.IsNullOrWhiteSpace(memoryContext))
        {
            sb.AppendLine();
            sb.AppendLine($"AVAILABLE MEMORIES:\n{memoryContext}");
        }

        if (reasoning.Thoughts.Any())
        {
            sb.AppendLine();
            sb.AppendLine("PREVIOUS REASONING:");
            for (int i = 0; i < reasoning.Thoughts.Count; i++)
            {
                var t = reasoning.Thoughts[i];
                sb.AppendLine($"  [{i + 1}] {t.Action}: {t.Analysis} (confidence: {t.Confidence:P0})");
            }
        }

        if (reasoning.Observations.Any())
        {
            sb.AppendLine();
            sb.AppendLine("OBSERVATIONS:");
            foreach (var obs in reasoning.Observations)
            {
                sb.AppendLine($"  - {obs}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("AVAILABLE ACTIONS:");
        sb.AppendLine("  - respond: Ready to generate response (include confidence 0.0-1.0)");
        sb.AppendLine("  - recall_memory: Search for specific memories (specify search query)");
        sb.AppendLine("  - store_memory: Save important information to long-term memory (specify what to remember)");
        sb.AppendLine("  - analyze_context: Deeper analysis of conversation flow");
        sb.AppendLine("  - consider_tone: Determine appropriate emotional tone");
        sb.AppendLine("  - reflect: Self-evaluate reasoning so far");
        sb.AppendLine("  - clarify: Note something that needs user clarification");
        sb.AppendLine("  - web_search: Search the web for real-time information (specify search query)");
        sb.AppendLine();
        sb.AppendLine("Respond with JSON:");
        sb.AppendLine("{");
        sb.AppendLine("  \"analysis\": \"your brief reasoning about the situation\",");
        sb.AppendLine("  \"action\": \"respond|recall_memory|store_memory|analyze_context|consider_tone|reflect|clarify|web_search\",");
        sb.AppendLine("  \"action_target\": \"specific target for the action (search query, what to remember, etc.)\",");
        sb.AppendLine("  \"confidence\": 0.0-1.0,");
        sb.AppendLine("  \"reasoning_note\": \"optional: why you chose this action\"");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Analyzes conversation context for patterns and topics.
    /// </summary>
    private string AnalyzeConversationContext(List<SessionMessage> messages, string? focus)
    {
        if (messages.Count < 2)
            return "New conversation, no established context";

        var userMessages = messages.Where(m => m.Role == "user").ToList();
        var assistantMessages = messages.Where(m => m.Role == "assistant").ToList();

        var insights = new List<string>();

        // Analyze conversation length
        if (messages.Count > 10)
            insights.Add("Extended conversation");
        else if (messages.Count > 4)
            insights.Add("Ongoing conversation");
        else
            insights.Add("Early conversation");

        // Analyze question patterns
        var questionCount = userMessages.Count(m => m.Content.Contains('?'));
        if (questionCount > userMessages.Count / 2)
            insights.Add("User is asking many questions");

        // Analyze message lengths
        var avgUserLength = userMessages.Any() ? userMessages.Average(m => m.Content.Length) : 0;
        if (avgUserLength > 200)
            insights.Add("User writes detailed messages");
        else if (avgUserLength < 50)
            insights.Add("User prefers brief messages");

        return string.Join("; ", insights);
    }

    /// <summary>
    /// Analyzes the appropriate tone for the response.
    /// </summary>
    private string AnalyzeTone(string userMessage, List<SessionMessage> messages)
    {
        var indicators = new List<string>();

        // Check for emotional indicators
        var lowerMessage = userMessage.ToLowerInvariant();

        if (lowerMessage.Contains("lol") || lowerMessage.Contains("haha") || lowerMessage.Contains("😂") || lowerMessage.Contains(":)"))
            indicators.Add("playful/humorous");
        if (lowerMessage.Contains("help") || lowerMessage.Contains("please") || lowerMessage.Contains("need"))
            indicators.Add("helpful/supportive");
        if (lowerMessage.Contains("?") && userMessage.Length < 50)
            indicators.Add("concise/direct");
        if (lowerMessage.Contains("thanks") || lowerMessage.Contains("appreciate"))
            indicators.Add("warm/friendly");
        if (userMessage.Any(char.IsUpper) && userMessage.Count(char.IsUpper) > userMessage.Length / 3)
            indicators.Add("enthusiastic");

        if (!indicators.Any())
            indicators.Add("neutral/conversational");

        return string.Join(", ", indicators);
    }

    /// <summary>
    /// Performs self-reflection on the reasoning process.
    /// </summary>
    private async Task<string> PerformSelfReflectionAsync(ReActReasoning reasoning, string userMessage, string? provider)
    {
        var reflectionPrompt = new StringBuilder();
        reflectionPrompt.AppendLine("Briefly evaluate this reasoning chain (1-2 sentences):");
        reflectionPrompt.AppendLine($"User asked: \"{userMessage}\"");
        reflectionPrompt.AppendLine("Reasoning steps:");
        foreach (var thought in reasoning.Thoughts)
        {
            reflectionPrompt.AppendLine($"  - {thought.Action}: {thought.Analysis}");
        }
        reflectionPrompt.AppendLine("Is this reasoning helpful? What's missing?");

        try
        {
            var response = await _coreChatService.CoreRequestAsync(
                reflectionPrompt.ToString(),
                customPersona: null,
                tokenLimit: 100,
                provider: provider);

            return response.Content.Length > 150 ? response.Content[..150] : response.Content;
        }
        catch
        {
            return "Reflection skipped due to error";
        }
    }

    /// <summary>
    /// Builds enriched context from reasoning observations.
    /// </summary>
    private string? BuildEnrichedContext(string? memoryContext, ReActReasoning reasoning)
    {
        var contextParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(memoryContext))
        {
            contextParts.Add(memoryContext);
        }

        // Add reasoning insights
        if (reasoning.Observations.Any())
        {
            var relevantObservations = reasoning.Observations
                .Where(o => !o.StartsWith("Low confidence"))
                .ToList();

            if (relevantObservations.Any())
            {
                contextParts.Add($"[Reasoning insights]\n{string.Join("\n", relevantObservations.Select(o => $"- {o}"))}");
            }
        }

        // Add tone suggestion if determined
        if (!string.IsNullOrEmpty(reasoning.SuggestedTone))
        {
            contextParts.Add($"[Suggested tone: {reasoning.SuggestedTone}]");
        }

        // Add clarification note if needed
        if (reasoning.NeedsClarification && !string.IsNullOrEmpty(reasoning.ClarificationTopic))
        {
            contextParts.Add($"[Note: Consider asking about '{reasoning.ClarificationTopic}' if response is uncertain]");
        }

        return contextParts.Any() ? string.Join("\n\n", contextParts) : null;
    }

    /// <summary>
    /// Parses the thought response from the LLM.
    /// </summary>
    private ReActThought ParseThought(string response)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var parsed = JsonSerializer.Deserialize<ReActThoughtDto>(json, options);

                if (parsed != null)
                {
                    return new ReActThought
                    {
                        Analysis = parsed.Analysis ?? "Unable to analyze",
                        Action = ParseAction(parsed.Action),
                        ActionTarget = parsed.ActionTarget ?? string.Empty,
                        Confidence = Math.Clamp(parsed.Confidence ?? 0.5f, 0f, 1f),
                        ReasoningNote = parsed.ReasoningNote
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse ReAct thought JSON, using fallback");
        }

        // Fallback: treat the whole response as analysis and default to respond
        return new ReActThought
        {
            Analysis = response.Length > 200 ? response[..200] : response,
            Action = ReActAction.Respond,
            ActionTarget = string.Empty,
            Confidence = 0.5f
        };
    }

    private static ReActAction ParseAction(string? action)
    {
        return action?.ToLowerInvariant().Replace("_", "") switch
        {
            "recallmemory" or "memory" => ReActAction.RecallMemory,
            "storememory" or "remember" or "store" or "save" or "savememory" => ReActAction.StoreMemory,
            "analyzecontext" or "context" or "analyze" => ReActAction.AnalyzeContext,
            "considertone" or "tone" => ReActAction.ConsiderTone,
            "reflect" or "selfreflect" or "evaluate" => ReActAction.Reflect,
            "clarify" or "ask" or "question" => ReActAction.Clarify,
            "websearch" or "search" or "web" or "lookup" or "findonline" => ReActAction.WebSearch,
            _ => ReActAction.Respond
        };
    }

    // ReAct support classes
    private class ReActReasoning
    {
        public List<ReActThought> Thoughts { get; } = new();
        public List<string> Observations { get; } = new();
        public string FinalAction { get; set; } = string.Empty;
        public float FinalConfidence { get; set; }
        public string? SuggestedTone { get; set; }
        public bool NeedsClarification { get; set; }
        public string? ClarificationTopic { get; set; }
    }

    private class ReActThought
    {
        public string Analysis { get; set; } = string.Empty;
        public ReActAction Action { get; set; } = ReActAction.Respond;
        public string ActionTarget { get; set; } = string.Empty;
        public float Confidence { get; set; } = 0.5f;
        public string? ReasoningNote { get; set; }
    }

    private class ReActThoughtDto
    {
        public string? Analysis { get; set; }
        public string? Action { get; set; }
        public string? ActionTarget { get; set; }
        public float? Confidence { get; set; }
        public string? ReasoningNote { get; set; }
    }

    private enum ReActAction
    {
        Respond,
        RecallMemory,
        StoreMemory,
        AnalyzeContext,
        ConsiderTone,
        Reflect,
        Clarify,
        WebSearch
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

        _logger.LogInformation("ServerMeta for {InstanceId}: PreferredProvider={Provider}, PreferredModel={Model}",
            instanceId, serverMeta?.PreferredProvider ?? "(null)", serverMeta?.PreferredModel ?? "(null)");

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

    /// <summary>
    /// Determines the appropriate memory type based on content
    /// </summary>
    private string DetermineMemoryType(string content)
    {
        var lowerContent = content.ToLowerInvariant();

        // Check for preferences
        if (lowerContent.Contains("prefer") ||
            lowerContent.Contains("like") ||
            lowerContent.Contains("don't like") ||
            lowerContent.Contains("favorite") ||
            lowerContent.Contains("hate"))
        {
            return "preference";
        }

        // Check for facts/information
        if (lowerContent.Contains("is ") ||
            lowerContent.Contains("are ") ||
            lowerContent.Contains("was ") ||
            lowerContent.Contains("were ") ||
            lowerContent.Contains("my name") ||
            lowerContent.Contains("i am") ||
            lowerContent.Contains("i'm"))
        {
            return "fact";
        }

        // Check for instructions/reminders
        if (lowerContent.Contains("remember to") ||
            lowerContent.Contains("remind me") ||
            lowerContent.Contains("don't forget") ||
            lowerContent.Contains("always") ||
            lowerContent.Contains("never"))
        {
            return "instruction";
        }

        // Default to context
        return "context";
    }
}