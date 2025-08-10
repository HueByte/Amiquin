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
            
            // 5. Send to LLM with fallback support
            var response = await _coreChatService.ChatAsync(
                instanceId,
                messages,
                customPersona: serverPersona,
                sessionContext: sessionContext,
                provider: provider);

            // 6. Store the exchange
            await StoreMessageExchangeAsync(instanceId, userId, botId, message, response.Content);

            // 7. Check if optimization is needed
            if (response.TotalTokens.HasValue && response.TotalTokens > _botOptions.MaxTokens * 0.8)
            {
                _logger.LogInformation("Token limit approaching for instance {InstanceId}, triggering optimization", instanceId);
                await OptimizeHistoryAsync(instanceId, messages, sessionContext);
            }

            _logger.LogInformation("Chat completed for instance {InstanceId}", instanceId);
            
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat for instance {InstanceId}", instanceId);
            return "I'm sorry, I encountered an error processing your message. Please try again.";
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

        // Store in cache
        await _messageCache.AddChatExchangeAsync(
            instanceId, 
            new List<OpenAI.Chat.ChatMessage> { userChatMessage, assistantChatMessage },
            messages);

        // Store in database
        var messageRepository = _serviceProvider.GetRequiredService<IMessageRepository>();
        await messageRepository.AddRangeAsync(messages);
        await messageRepository.SaveChangesAsync();
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
            var summaryResponse = await _coreChatService.CoreRequestAsync(
                summaryPrompt,
                tokenLimit: 400);

            // Update session context
            var newContext = string.IsNullOrWhiteSpace(existingContext)
                ? summaryResponse.Content
                : $"{existingContext}\n\n{summaryResponse.Content}";

            // Check if context needs self-summarization
            if (newContext.Length > 2000) // Rough check
            {
                var consolidatePrompt = $"Consolidate these conversation summaries into one concise summary (max 400 tokens):\n\n{newContext}";
                var consolidatedResponse = await _coreChatService.CoreRequestAsync(
                    consolidatePrompt,
                    tokenLimit: 400);
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
}