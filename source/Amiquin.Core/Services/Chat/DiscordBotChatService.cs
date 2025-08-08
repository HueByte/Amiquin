using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Discord bot chat service that provides a simple interface for handling chat messages
/// with session management and conversation history.
/// </summary>
public class DiscordBotChatService : IDiscordBotChatService
{
    private readonly ILogger<DiscordBotChatService> _logger;
    private readonly IConversationCoreService _conversationCoreService;
    private readonly ISessionManager _sessionManager;
    private readonly IMessageHistoryManager _messageHistoryManager;
    private readonly IHistoryOptimizerService _historyOptimizerService;

    public DiscordBotChatService(
        ILogger<DiscordBotChatService> logger,
        IConversationCoreService conversationCoreService,
        ISessionManager sessionManager,
        IMessageHistoryManager messageHistoryManager,
        IHistoryOptimizerService historyOptimizerService)
    {
        _logger = logger;
        _conversationCoreService = conversationCoreService;
        _sessionManager = sessionManager;
        _messageHistoryManager = messageHistoryManager;
        _historyOptimizerService = historyOptimizerService;
    }

    /// <summary>
    /// Processes a Discord message and returns the AI response with full session management.
    /// </summary>
    /// <param name="userMessage">The user's message content</param>
    /// <param name="userId">Discord user ID for session management</param>
    /// <param name="channelId">Discord channel ID for context separation</param>
    /// <param name="guildId">Discord guild ID (optional, for server-specific sessions)</param>
    /// <returns>AI response string</returns>
    public async Task<string> ProcessMessageAsync(string userMessage, ulong userId, ulong channelId, ulong? guildId = null)
    {
        try
        {
            _logger.LogInformation("Processing Discord message from user {UserId} in channel {ChannelId}", userId, channelId);

            // Create session identifier
            var sessionId = GenerateSessionId(userId, channelId, guildId);

            // Get or create session with message history
            var session = await _sessionManager.GetOrCreateSessionAsync(sessionId, userId.ToString());
            
            // Load message history for context
            var messageHistory = await _messageHistoryManager.GetMessageHistoryAsync(sessionId);
            
            // Add user message to history
            var userChatMessage = ChatMessage.CreateUserMessage(userMessage);
            messageHistory.Add(userChatMessage);

            // Get AI response
            var response = await _conversationCoreService.ChatAsync(sessionId, messageHistory);
            var assistantResponse = response.Content.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(assistantResponse))
            {
                _logger.LogWarning("Empty response received for session {SessionId}", sessionId);
                return "I'm sorry, I couldn't generate a response right now. Please try again.";
            }

            // Add assistant message to history
            var assistantChatMessage = ChatMessage.CreateAssistantMessage(assistantResponse);
            messageHistory.Add(assistantChatMessage);

            // Save the conversation exchange
            await _messageHistoryManager.SaveMessageExchangeAsync(sessionId, userChatMessage, assistantChatMessage);

            // Check if history optimization is needed
            var tokenUsage = response.Usage;
            if (tokenUsage != null && _historyOptimizerService.ShouldOptimizeMessageHistory(tokenUsage))
            {
                try
                {
                    var optimizationResult = await _historyOptimizerService.OptimizeMessageHistory(
                        tokenUsage.TotalTokenCount, 
                        messageHistory);
                    
                    await _messageHistoryManager.OptimizeHistoryAsync(sessionId, optimizationResult.RemovedMessages);
                    _logger.LogInformation("Optimized message history for session {SessionId}, removed {Count} messages", 
                        sessionId, optimizationResult.RemovedMessages);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to optimize message history for session {SessionId}", sessionId);
                }
            }

            _logger.LogInformation("Successfully processed message for session {SessionId}", sessionId);
            return assistantResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Discord message from user {UserId}", userId);
            return "I encountered an error while processing your message. Please try again later.";
        }
    }

    /// <summary>
    /// Processes a simple message without session management (stateless).
    /// </summary>
    /// <param name="userMessage">The user's message content</param>
    /// <returns>AI response string</returns>
    public async Task<string> ProcessSimpleMessageAsync(string userMessage)
    {
        try
        {
            _logger.LogInformation("Processing simple message (stateless)");
            
            var response = await _conversationCoreService.ExchangeMessageAsync(userMessage);
            
            _logger.LogInformation("Successfully processed simple message");
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing simple message");
            return "I encountered an error while processing your message. Please try again later.";
        }
    }

    /// <summary>
    /// Clears conversation history for a specific user/channel combination.
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="channelId">Discord channel ID</param>
    /// <param name="guildId">Discord guild ID (optional)</param>
    public async Task ClearConversationAsync(ulong userId, ulong channelId, ulong? guildId = null)
    {
        var sessionId = GenerateSessionId(userId, channelId, guildId);
        await _messageHistoryManager.ClearHistoryAsync(sessionId);
        _logger.LogInformation("Cleared conversation history for session {SessionId}", sessionId);
    }

    /// <summary>
    /// Gets conversation statistics for a session.
    /// </summary>
    /// <param name="userId">Discord user ID</param>
    /// <param name="channelId">Discord channel ID</param>
    /// <param name="guildId">Discord guild ID (optional)</param>
    /// <returns>Session statistics</returns>
    public async Task<SessionStats> GetConversationStatsAsync(ulong userId, ulong channelId, ulong? guildId = null)
    {
        var sessionId = GenerateSessionId(userId, channelId, guildId);
        return await _messageHistoryManager.GetSessionStatsAsync(sessionId);
    }

    private string GenerateSessionId(ulong userId, ulong channelId, ulong? guildId)
    {
        // Create a unique session ID that combines user, channel, and optionally guild
        // This allows for separate conversations per channel while maintaining user context
        var parts = new List<string> { userId.ToString(), channelId.ToString() };
        
        if (guildId.HasValue)
        {
            parts.Add(guildId.Value.ToString());
        }
        
        return string.Join(":", parts);
    }
}