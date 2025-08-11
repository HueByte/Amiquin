using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Amiquin.Core.Services.ChatContext;

public class ChatContextService : IChatContextService
{
    private readonly ILogger<ChatContextService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    public readonly ConcurrentDictionary<ulong, ConcurrentBag<SocketMessage>> Messages = new();

    // Track engagement multipliers per guild (higher = more engagement)
    private readonly ConcurrentDictionary<ulong, float> _engagementMultipliers = new();

    // Track recent activity timestamps for real-time activity detection
    private readonly ConcurrentDictionary<ulong, Queue<DateTime>> _activityTimestamps = new();

    // Track semaphores for each guild to prevent concurrent processing
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _guildSemaphores = new();

    public ChatContextService(ILogger<ChatContextService> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task HandleUserMessageAsync(ulong scopeId, SocketMessage socketMessage)
    {
        var message = socketMessage.Content.Trim();
        if (socketMessage.Author.IsBot || string.IsNullOrWhiteSpace(message))
        {
            _logger.LogDebug("Ignoring message from bot or empty message in scope {ScopeId}", scopeId);
            return Task.CompletedTask;
        }

        // Filter out messages from excluded channels (moderation, admin, bot channels)
        if (IsChannelExcludedFromActivity(socketMessage.Channel))
        {
            _logger.LogDebug("Ignoring message from excluded channel {ChannelName} in scope {ScopeId}",
                socketMessage.Channel.Name, scopeId);
            return Task.CompletedTask;
        }

        var username = socketMessage.Author.GlobalName ?? socketMessage.Author.Username;
        _logger.LogDebug("Received message from {Username} in scope {ScopeId}: {Message}", username, scopeId, message);

        // Track activity timestamp for real-time detection
        TrackActivityTimestamp(scopeId);

        // Check if bot was mentioned and increase engagement
        CheckForBotMentionAndIncreaseEngagement(scopeId, socketMessage);

        // Check for sudden activity spike and potentially trigger immediate engagement
        _ = Task.Run(() => CheckActivitySpikeAsync(scopeId, socketMessage));

        return AddMessageToContextAsync(scopeId, socketMessage);
    }

    private Task AddMessageToContextAsync(ulong scopeId, SocketMessage socketMessage)
    {
        _logger.LogInformation("Adding message to context for scope {ScopeId} from {Username}", scopeId, socketMessage.Author.Username);
        if (!Messages.ContainsKey(scopeId))
        {
            Messages[scopeId] = new ConcurrentBag<SocketMessage>();
        }

        Messages[scopeId].Add(socketMessage);

        return Task.CompletedTask;
    }

    public string[] GetContextMessages(ulong scopeId)
    {
        if (Messages.TryGetValue(scopeId, out var messages))
        {
            return messages.Select(m => m.Content.Trim()).ToArray();
        }

        _logger.LogWarning("No messages found for scope {ScopeId}", scopeId);
        return Array.Empty<string>();
    }

    /// <summary>
    /// Clears the context messages for a scope after successful engagement
    /// </summary>
    public void ClearContextMessages(ulong scopeId)
    {
        Messages.TryRemove(scopeId, out _);
        _logger.LogInformation("Cleared messages for scope {ScopeId} after engagement", scopeId);
    }

    #region Actions to engage with users
    public async Task<string?> StartTopicAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Starting random topic for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var prompt = "[System]: Provide a fun, interesting, or thought-provoking conversation starter. " +
                        "Keep it casual and engaging, something that would naturally start a discussion in a Discord server. " +
                        "Don't use quotation marks. Just provide the message directly.";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    var chunks = Utilities.DiscordUtilities.ChunkMessage(response);

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        if (chunks.Count > 1)
                        {
                            chunk += $" `({i + 1}/{chunks.Count})`";
                        }

                        await targetChannel.SendMessageAsync(chunk);

                        if (i < chunks.Count - 1)
                        {
                            await Task.Delay(500);
                        }
                    }

                    _logger.LogInformation("Started topic in guild {GuildId} with {ChunkCount} message(s): {Topic}",
                        guildId, chunks.Count, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting topic for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> AnswerMentionAsync(ulong guildId, SocketMessage mentionMessage, IMessageChannel? channel = null)
    {
        // Get or create semaphore for this guild
        var semaphore = _guildSemaphores.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));

        // Try to acquire the semaphore (non-blocking)
        if (!await semaphore.WaitAsync(0))
        {
            _logger.LogInformation("Skipping mention response for guild {GuildId} - already processing a message", guildId);
            return null; // Skip this message as we're already processing one
        }

        IDisposable? typingState = null;

        try
        {
            _logger.LogInformation("Answering mention in guild {GuildId} from {Username}",
                guildId, mentionMessage.Author.Username);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var context = FormatContextMessagesForAI(guildId);
            var username = mentionMessage.Author.GlobalName ?? mentionMessage.Author.Username;
            var userId = mentionMessage.Author.Id;
            var mentionContent = mentionMessage.Content.Trim();

            // Format with user metadata: [username:userId] message, add context if available
            var contextPrompt = !string.IsNullOrWhiteSpace(context) ? $"\nRecent context:\n{context}" : "";
            var mentionGuidance = "\n\nIMPORTANT: When addressing users, ALWAYS use Discord mention format <@userId>. " +
                                  "To mention the user who mentioned you, use <@" + userId + ">. " +
                                  "For other users in context, use their userId from [username:userId] format.";
            var prompt = $"[{username}:{userId}] {mentionContent}{contextPrompt}{mentionGuidance}";

            // Start typing indicator
            var targetChannel = channel ?? mentionMessage.Channel as IMessageChannel;
            if (targetChannel != null)
            {
                typingState = targetChannel.EnterTypingState();
            }

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, mentionMessage.Author.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                if (targetChannel != null)
                {
                    // Use simple text chunking to handle long responses
                    var chunks = Utilities.DiscordUtilities.ChunkMessage(response, 1000);

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        // Add part indicator if multiple chunks
                        if (chunks.Count > 1)
                        {
                            chunk += $" `({i + 1}/{chunks.Count})`";
                        }

                        // Send message with reference to original message for first chunk
                        await targetChannel.SendMessageAsync(chunk, messageReference: i == 0 ? new Discord.MessageReference(mentionMessage.Id) : null);

                        // Small delay between chunks
                        if (i < chunks.Count - 1)
                        {
                            await Task.Delay(500);
                        }
                    }

                    _logger.LogInformation("Replied to mention from {Username} in guild {GuildId} with {ChunkCount} message(s): {Response}",
                        username, guildId, chunks.Count, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }

                // Clear context messages after successful mention response to prevent duplicate responses
                ClearContextMessages(guildId);

                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering mention for guild {GuildId}", guildId);
            return null;
        }
        finally
        {
            // Stop typing indicator
            typingState?.Dispose();

            // Release the semaphore
            semaphore.Release();
        }
    }

    public async Task<string?> AskQuestionAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Asking engaging question for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var context = FormatContextMessagesForAI(guildId);
            var hasActiveContext = !string.IsNullOrWhiteSpace(context);

            string basePrompt;
            if (hasActiveContext)
            {
                // Active chat: ask follow-up or related questions
                basePrompt = "[System]: Based on the ongoing conversation, ask a relevant follow-up question " +
                           "or ask for others' opinions on what's being discussed. " +
                           "Make it natural and engaging, like you're genuinely curious about the topic.";
            }
            else
            {
                // Low activity: ask broader engaging questions
                basePrompt = "[System]: Ask an engaging question that would spark discussion. " +
                           "Make it fun, interesting, or thought-provoking that the community would enjoy discussing.";
            }

            var mentionGuidance = hasActiveContext
                ? "\n\nIMPORTANT: When referring to users, ALWAYS use Discord mention format <@userId>. " +
                  "Messages are formatted as [username:userId] content, so use the userId for mentions. " +
                  "For example: if message shows [John:123456789] hello, respond with 'Hey <@123456789>, ...' NOT 'Hey John, ...'"
                : "\n\nIMPORTANT: When you want to address users in your message, use Discord mention format <@userId>.";

            var prompt = hasActiveContext
                ? $"{basePrompt} Here's the current conversation:\n{context}{mentionGuidance}"
                : $"{basePrompt}{mentionGuidance}";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    var chunks = Utilities.DiscordUtilities.ChunkMessage(response);

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        if (chunks.Count > 1)
                        {
                            chunk += $" `({i + 1}/{chunks.Count})`";
                        }

                        await targetChannel.SendMessageAsync(chunk);

                        if (i < chunks.Count - 1)
                        {
                            await Task.Delay(500);
                        }
                    }

                    _logger.LogInformation("Asked question in guild {GuildId} with {ChunkCount} message(s): {Question}",
                        guildId, chunks.Count, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error asking question for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> ShareInterestingContentAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Sharing interesting content for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var prompt = "[System]: Share something interesting, educational, or thought-provoking. " +
                        "It could be a fun fact, an interesting observation about technology/gaming/life, " +
                        "or something that would spark curiosity. Keep it engaging and conversational. " +
                        "Don't use quotation marks.";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    var chunks = Utilities.DiscordUtilities.ChunkMessage(response);

                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        if (chunks.Count > 1)
                        {
                            chunk += $" `({i + 1}/{chunks.Count})`";
                        }

                        await targetChannel.SendMessageAsync(chunk);

                        if (i < chunks.Count - 1)
                        {
                            await Task.Delay(500);
                        }
                    }

                    _logger.LogInformation("Shared interesting content in guild {GuildId} with {ChunkCount} message(s): {Content}",
                        guildId, chunks.Count, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing interesting content for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> ShareFunnyContentAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Sharing funny content for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();
            var botContextAccessor = scope.ServiceProvider.GetRequiredService<BotContextAccessor>();

            var prompt = "[System]: Provide a funny message, joke, or humorous observation. " +
                        "Keep it light-hearted, appropriate for Discord, and entertaining. " +
                        "It could be about gaming, tech, everyday life, or just a clever observation. " +
                        "Don't use quotation marks.";

            try
            {
                // Use session-based chat service like /chat command with proper BotContextAccessor management
                var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    var targetChannel = await GetTargetChannelAsync(guildId, channel);
                    if (targetChannel != null)
                    {
                        var chunks = Utilities.DiscordUtilities.ChunkMessage(response);

                        for (int i = 0; i < chunks.Count; i++)
                        {
                            var chunk = chunks[i];
                            if (chunks.Count > 1)
                            {
                                chunk += $" `({i + 1}/{chunks.Count})`";
                            }

                            await targetChannel.SendMessageAsync(chunk);

                            if (i < chunks.Count - 1)
                            {
                                await Task.Delay(500);
                            }
                        }

                        _logger.LogInformation("Shared funny content in guild {GuildId} with {ChunkCount} message(s): {Content}",
                            guildId, chunks.Count, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                    }
                    return response;
                }

                return null;
            }
            finally
            {
                // Always finish the BotContextAccessor to prevent timeout
                botContextAccessor.Finish();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing funny content for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> ShareUsefulContentAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Sharing useful content for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();
            var botContextAccessor = scope.ServiceProvider.GetRequiredService<BotContextAccessor>();

            try
            {
                var prompt = "[System]: Share useful tips, advice, or helpful information. " +
                            "It could be about productivity, gaming tips, tech advice, Discord features, " +
                            "or general life hacks. Make it practical and valuable. " +
                            "Don't use quotation marks.";

                // Use session-based chat service like /chat command
                var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

                if (!string.IsNullOrWhiteSpace(response))
                {
                    var targetChannel = await GetTargetChannelAsync(guildId, channel);
                    if (targetChannel != null)
                    {
                        await targetChannel.SendMessageAsync(response);
                        _logger.LogInformation("Shared useful content in guild {GuildId}: {Content}",
                            guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                    }
                    return response;
                }

                return null;
            }
            finally
            {
                // Always finish the BotContextAccessor to prevent timeout
                botContextAccessor.Finish();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing useful content for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> ShareNewsAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Sharing news for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var prompt = "[System]: Share interesting tech news, gaming updates, or general interesting developments. " +
                        "Keep it conversational and engaging, focusing on things that would interest a Discord community. " +
                        "Don't make up specific facts - keep it general and discussion-oriented. " +
                        "Don't use quotation marks.";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Shared news in guild {GuildId}: {News}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing news for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> IncreaseEngagementAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Increasing engagement for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var context = FormatContextMessagesForAI(guildId);

            // Different behavior based on whether there's active conversation or not
            var hasActiveContext = !string.IsNullOrWhiteSpace(context);

            string basePrompt;
            if (hasActiveContext)
            {
                // Active chat: participate in the ongoing conversation
                basePrompt = "[System]: Join the ongoing conversation naturally. Give your opinion, ask a follow-up question, " +
                           "share a related thought, or add something relevant to what's being discussed. " +
                           "Be engaging and conversational, like you're part of the group chat. " +
                           "Don't announce you're joining - just participate naturally.";
            }
            else
            {
                // Low activity: spark new conversation
                basePrompt = "[System]: Create an engaging message to spark activity and conversation. " +
                           "This could be asking for opinions, starting a mini-game, creating a poll idea, " +
                           "or encouraging community interaction. Make it fun and inviting.";
            }

            var mentionGuidance = hasActiveContext
                ? "\n\nIMPORTANT: When referring to users, ALWAYS use Discord mention format <@userId>. " +
                  "Messages are formatted as [username:userId] content, so use the userId for mentions. " +
                  "For example: if message shows [John:123456789] hello, respond with 'Hey <@123456789>, ...' NOT 'Hey John, ...'"
                : "\n\nIMPORTANT: When you want to address users in your message, use Discord mention format <@userId>.";

            var prompt = hasActiveContext
                ? $"{basePrompt} Here's the current conversation:\n{context}{mentionGuidance}"
                : $"{basePrompt}{mentionGuidance}";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Increased engagement in guild {GuildId}: {Message}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error increasing engagement for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> ShareOpinionAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Sharing opinion for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var context = FormatContextMessagesForAI(guildId);
            var hasActiveContext = !string.IsNullOrWhiteSpace(context);

            string basePrompt;
            if (hasActiveContext)
            {
                // Active chat: share opinion on what's being discussed
                basePrompt = "[System]: Based on the ongoing conversation, share your honest opinion or perspective on the topic. " +
                           "Give a thoughtful take that adds to the discussion. Be genuine and conversational, " +
                           "like you're sharing your view with friends. Don't be neutral - have a stance, but be respectful.";
            }
            else
            {
                // Low activity: share opinion on general topics
                basePrompt = "[System]: Share an interesting opinion or perspective on a topic that might spark discussion. " +
                           "This could be about gaming, technology, current trends, or life in general. " +
                           "Make it thought-provoking and conversation-starting.";
            }

            var prompt = hasActiveContext
                ? $"{basePrompt} Here's what's being discussed:\n{context}"
                : basePrompt;

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Shared opinion in guild {GuildId}: {Opinion}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sharing opinion for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> AdaptiveResponseAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Generating adaptive response for guild {GuildId}", guildId);

            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();

            var context = FormatContextMessagesForAI(guildId);
            var hasActiveContext = !string.IsNullOrWhiteSpace(context);

            string basePrompt;
            if (hasActiveContext)
            {
                // Active chat: let AI decide best response based on context
                basePrompt = "[System]: Analyze the current conversation and respond in the most appropriate way. " +
                           "You could ask a follow-up question, share a related story, give advice, make a joke, " +
                           "share an opinion, or contribute in whatever way feels most natural and engaging. " +
                           "Choose your response style based on the tone and content of the conversation.";
            }
            else
            {
                // Low activity: let AI decide how to spark engagement
                basePrompt = "[System]: The chat is quiet. Decide the best way to engage the community. " +
                           "You could start a topic, ask a question, share something interesting, tell a joke, " +
                           "or do whatever feels right to get people talking. Choose your approach freely.";
            }

            var mentionGuidance = hasActiveContext
                ? "\n\nIMPORTANT: When referring to users, ALWAYS use Discord mention format <@userId>. " +
                  "Messages are formatted as [username:userId] content, so use the userId for mentions. " +
                  "For example: if message shows [John:123456789] hello, respond with 'Hey <@123456789>, ...' NOT 'Hey John, ...'"
                : "\n\nIMPORTANT: When you want to address users in your message, use Discord mention format <@userId>.";

            var prompt = hasActiveContext
                ? $"{basePrompt} Here's the current conversation:\n{context}{mentionGuidance}"
                : $"{basePrompt}{mentionGuidance}";

            // Use session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guildId, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Sent adaptive response in guild {GuildId}: {Response}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating adaptive response for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task InitializeActivityContextAsync(SocketGuild guild)
    {
        try
        {
            _logger.LogInformation("Initializing activity context for guild {GuildName} ({GuildId})", guild.Name, guild.Id);

            // Find the general channel (prefer "general", "main", "chat", etc.)
            var generalChannel = FindGeneralChannel(guild);
            if (generalChannel == null)
            {
                _logger.LogWarning("No suitable general channel found in guild {GuildName} ({GuildId})", guild.Name, guild.Id);
                return;
            }

            _logger.LogDebug("Found general channel: {ChannelName} in guild {GuildName}", generalChannel.Name, guild.Name);

            // Get the last 10 messages from the general channel
            var messageBatches = await generalChannel.GetMessagesAsync(10, CacheMode.AllowDownload).ToListAsync();
            var messages = messageBatches.SelectMany(x => x).OrderBy(m => m.Timestamp).ToArray();

            var processedCount = 0;
            foreach (var message in messages)
            {
                // Convert to SocketMessage for processing (we need to simulate the message structure)
                if (message is SocketMessage socketMessage)
                {
                    // Add message to context without triggering activity spike checks or engagement
                    await AddMessageToContextAsync(guild.Id, socketMessage);
                    processedCount++;
                }
            }

            _logger.LogInformation("Initialized activity context for guild {GuildName} with {Count} messages from #{ChannelName}",
                guild.Name, processedCount, generalChannel.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing activity context for guild {GuildName} ({GuildId})", guild.Name, guild.Id);
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Determines if a channel should be excluded from activity monitoring.
    /// Excludes moderation, admin, private, and bot channels.
    /// </summary>
    private bool IsChannelExcludedFromActivity(IChannel channel)
    {
        // TODO: Have server admins to configure which channels can be used for message collecting
        if (channel is not ITextChannel textChannel)
        {
            _logger.LogDebug("Channel {ChannelName} excluded: not a text channel", channel.Name);
            return true;
        }

        var channelName = textChannel.Name.ToLowerInvariant();

        // Moderation and admin channels
        var moderationKeywords = new[]
        {
            "mod", "admin", "staff", "moderator", "moderation", "management",
            "private", "team", "internal", "restricted", "official"
        };

        // Bot and system channels  
        var botKeywords = new[]
        {
            "bot", "command", "cmd", "log", "logs", "audit", "system",
            "welcome", "rules", "announcements", "updates"
        };

        // Check if channel name contains excluded keywords
        if (moderationKeywords.Any(keyword => channelName.Contains(keyword)) ||
            botKeywords.Any(keyword => channelName.Contains(keyword)))
        {
            _logger.LogDebug("Channel {ChannelName} excluded: contains moderation/bot keywords", textChannel.Name);
            return true;
        }

        // Additional check: exclude channels that are obviously private/restricted based on permissions
        // Only exclude if @everyone explicitly has SendMessages denied AND the channel name suggests it's restricted
        var everyoneRole = textChannel.Guild.EveryoneRole;
        var permissions = textChannel.GetPermissionOverwrite(everyoneRole);

        if (permissions.HasValue && permissions.Value.SendMessages == PermValue.Deny &&
            (channelName.Contains("private") || channelName.Contains("staff") || channelName.Contains("admin")))
        {
            _logger.LogDebug("Channel {ChannelName} excluded: permission-based restriction", textChannel.Name);
            return true;
        }

        _logger.LogDebug("Channel {ChannelName} included in activity monitoring", textChannel.Name);
        return false;
    }

    /// <summary>
    /// Finds the most suitable "general" channel in a guild for initialization
    /// </summary>
    private ITextChannel? FindGeneralChannel(SocketGuild guild)
    {
        // Priority order for finding a general channel
        var preferredNames = new[] { "general", "main", "chat", "lobby", "discussion", "public" };

        // First, try to find channels with preferred names, excluding moderation/admin channels
        foreach (var name in preferredNames)
        {
            var channel = guild.TextChannels.FirstOrDefault(c =>
                c.Name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
                !IsChannelExcludedFromActivity(c) &&
                guild.CurrentUser.GetPermissions(c).ReadMessageHistory &&
                guild.CurrentUser.GetPermissions(c).ViewChannel);
            if (channel != null)
                return channel;
        }

        // Fallback to the first text channel the bot can read from, excluding restricted channels
        return guild.TextChannels
            .Where(c => !IsChannelExcludedFromActivity(c) &&
                       guild.CurrentUser.GetPermissions(c).ReadMessageHistory &&
                       guild.CurrentUser.GetPermissions(c).ViewChannel)
            .OrderBy(c => c.Position)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets the target channel for sending messages - uses PrimaryChannelId from ServerMeta if configured,
    /// otherwise falls back to the provided channel or the default text channel
    /// </summary>
    private async Task<IMessageChannel?> GetTargetChannelAsync(ulong guildId, IMessageChannel? providedChannel = null)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();
            var serverMetaService = scope.ServiceProvider.GetRequiredService<IServerMetaService>();

            // Get the guild
            var guild = discordClient.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning("Could not find guild {GuildId}", guildId);
                return providedChannel;
            }

            // Try to get configured primary channel from ServerMeta
            try
            {
                var serverMeta = await serverMetaService.GetServerMetaAsync(guildId);
                if (serverMeta?.PrimaryChannelId.HasValue == true)
                {
                    var primaryChannel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
                    if (primaryChannel != null)
                    {
                        _logger.LogDebug("Using configured primary channel {ChannelName} for guild {GuildId}",
                            primaryChannel.Name, guildId);
                        return primaryChannel;
                    }
                    else
                    {
                        _logger.LogWarning("Configured primary channel {ChannelId} not found for guild {GuildId}",
                            serverMeta.PrimaryChannelId.Value, guildId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting server meta for guild {GuildId}", guildId);
            }

            // Fall back to provided channel if available
            if (providedChannel != null)
            {
                _logger.LogDebug("Using provided channel for guild {GuildId}", guildId);
                return providedChannel;
            }

            // Fall back to default text channel (first text channel the bot can send messages to, excluding restricted channels)
            var defaultChannel = guild.TextChannels
                .Where(c => !IsChannelExcludedFromActivity(c) &&
                           guild.CurrentUser.GetPermissions(c).SendMessages &&
                           guild.CurrentUser.GetPermissions(c).ViewChannel)
                .OrderBy(c => c.Position)
                .FirstOrDefault();

            if (defaultChannel != null)
            {
                _logger.LogDebug("Using default text channel {ChannelName} for guild {GuildId}",
                    defaultChannel.Name, guildId);
                return defaultChannel;
            }

            _logger.LogWarning("No suitable channel found for guild {GuildId}", guildId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting target channel for guild {GuildId}", guildId);
            return providedChannel;
        }
    }

    /// <summary>
    /// Checks if the bot was mentioned in a message and increases engagement multiplier for proactive responses
    /// </summary>
    private void CheckForBotMentionAndIncreaseEngagement(ulong scopeId, SocketMessage socketMessage)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var discordClient = scope.ServiceProvider.GetService<DiscordShardedClient>();

            if (discordClient?.CurrentUser == null)
            {
                _logger.LogWarning("Discord client or current user not available for mention check");
                return;
            }

            var botUserId = discordClient.CurrentUser.Id;
            var isMentioned = socketMessage.MentionedUserIds.Contains(botUserId);

            if (isMentioned)
            {
                _logger.LogInformation("Bot was mentioned by {Username} in scope {ScopeId}, increasing engagement multiplier",
                    socketMessage.Author.Username, scopeId);

                // Increase engagement multiplier when mentioned
                _engagementMultipliers.AddOrUpdate(scopeId, 2.0f, (key, current) => Math.Min(current + 0.5f, 3.0f));

                _logger.LogDebug("New engagement multiplier for scope {ScopeId}: {Multiplier}",
                    scopeId, _engagementMultipliers.GetValueOrDefault(scopeId, 1.0f));
            }
            else
            {
                // Gradually decrease engagement multiplier over time
                _engagementMultipliers.AddOrUpdate(scopeId, 1.0f, (key, current) => Math.Max(current * 0.98f, 1.0f));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for bot mention in scope {ScopeId}", scopeId);
        }
    }

    /// <summary>
    /// Gets the engagement multiplier for a specific scope
    /// </summary>
    public float GetEngagementMultiplier(ulong scopeId)
    {
        return _engagementMultipliers.GetValueOrDefault(scopeId, 1.0f);
    }

    /// <summary>
    /// Resets the engagement multiplier for a specific scope back to baseline (1.0).
    /// Also clears any context messages to give the bot a fresh start.
    /// </summary>
    public void ResetEngagement(ulong scopeId)
    {
        _logger.LogInformation("Resetting engagement for scope {ScopeId} to baseline", scopeId);

        // Reset engagement multiplier to baseline
        _engagementMultipliers.AddOrUpdate(scopeId, 1.0f, (key, current) => 1.0f);

        // Clear context messages for a fresh start
        ClearContextMessages(scopeId);

        // Clear activity timestamps as well
        if (_activityTimestamps.TryGetValue(scopeId, out var timestamps))
        {
            timestamps.Clear();
        }

        _logger.LogDebug("Engagement reset complete for scope {ScopeId}", scopeId);
    }

    /// <summary>
    /// Formats context messages with user metadata: [username:userId] [message content]
    /// This format enables user mentions via &lt;@userId&gt; syntax in AI responses
    /// </summary>
    public string FormatContextMessagesForAI(ulong scopeId)
    {
        try
        {
            if (!Messages.TryGetValue(scopeId, out var messages) || !messages.Any())
            {
                return string.Empty;
            }

            var contextLines = new List<string>();
            var recentMessages = messages.TakeLast(10).ToArray(); // Get last 10 messages for context

            foreach (var msg in recentMessages)
            {
                if (msg.Author.IsBot) continue; // Skip bot messages

                var username = msg.Author.GlobalName ?? msg.Author.Username;
                var userId = msg.Author.Id;
                var content = msg.Content.Trim();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    contextLines.Add($"[{username}:{userId}] {content}");
                }
            }

            return string.Join("\n", contextLines);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting context messages for scope {ScopeId}", scopeId);
            return string.Empty;
        }
    }

    /// <summary>
    /// Sends an AI-generated message to a Discord channel based on conversation context
    /// </summary>
    public async Task<string?> SendContextAwareMessage(SocketGuild guild, Discord.IMessageChannel channel)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var personaChatService = scope.ServiceProvider.GetRequiredService<IPersonaChatService>();
            var discordClient = scope.ServiceProvider.GetRequiredService<DiscordShardedClient>();

            // Format recent conversation context
            var contextMessages = FormatContextMessagesForAI(guild.Id);

            if (string.IsNullOrWhiteSpace(contextMessages))
            {
                _logger.LogDebug("No context messages available for guild {GuildId}", guild.Id);
                return null;
            }

            _logger.LogInformation("Generating context-aware message for guild {GuildName} with {MessageLength} chars of context",
                guild.Name, contextMessages.Length);

            // Create a prompt that encourages natural participation in the conversation
            var prompt = $"[System]: Based on the recent conversation context below, engage naturally as if you're part of the discussion. " +
                        $"Don't announce that you're responding to context. Just participate naturally in the ongoing conversation:\n\n{contextMessages}";

            // Get AI response using the session-based chat service like /chat command
            var response = await personaChatService.ChatAsync(guild.Id, discordClient.CurrentUser.Id, discordClient.CurrentUser.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                // Send the message to the channel
                await channel.SendMessageAsync(response);
                _logger.LogInformation("Sent context-aware message to {ChannelName} in {GuildName}: {Response}",
                    channel.Name, guild.Name, response.Substring(0, Math.Min(response.Length, 100)) + "...");

                // Clear context messages after successful engagement
                ClearContextMessages(guild.Id);

                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending context-aware message to guild {GuildId}", guild.Id);
            return null;
        }
    }

    /// <summary>
    /// Tracks activity timestamp for real-time activity detection
    /// </summary>
    private void TrackActivityTimestamp(ulong scopeId)
    {
        var now = DateTime.UtcNow;
        var timestamps = _activityTimestamps.GetOrAdd(scopeId, _ => new Queue<DateTime>());

        lock (timestamps)
        {
            timestamps.Enqueue(now);

            // Keep only last 2 minutes of activity
            var cutoff = now.AddMinutes(-2);
            while (timestamps.Count > 0 && timestamps.Peek() < cutoff)
            {
                timestamps.Dequeue();
            }
        }
    }

    /// <summary>
    /// Calculates current real-time activity level based on recent messages
    /// </summary>
    public double GetCurrentActivityLevel(ulong scopeId)
    {
        if (!_activityTimestamps.TryGetValue(scopeId, out var timestamps))
            return 0.1;

        lock (timestamps)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-1); // Last 1 minute
            var recentCount = timestamps.Count(t => t > cutoff);

            return recentCount switch
            {
                <= 0 => 0.1,        // Very low (handles negative values)
                1 => 0.3,           // Low  
                2 => 0.5,           // Below normal
                3 => 0.7,           // Normal
                4 => 1.0,           // Good
                5 => 1.3,           // High
                6 => 1.5,           // Very high
                _ => 2.0            // Extremely high (7 or more)
            };
        }
    }

    /// <summary>
    /// Checks for sudden activity spikes and triggers immediate engagement
    /// </summary>
    private async Task CheckActivitySpikeAsync(ulong scopeId, SocketMessage socketMessage)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var toggleService = scope.ServiceProvider.GetService<IToggleService>();

            if (toggleService == null || !await toggleService.IsEnabledAsync(scopeId, Constants.ToggleNames.EnableLiveJob))
                return;

            var currentActivity = GetCurrentActivityLevel(scopeId);
            var previousMultiplier = _engagementMultipliers.GetValueOrDefault(scopeId, 1.0f);

            // Detect activity spike (sudden jump to high activity)
            var isActivitySpike = currentActivity >= 1.3 && previousMultiplier < 1.5f;

            // Random chance for immediate engagement on activity spikes (10% chance - reduced from 30%)
            if (isActivitySpike && new Random().NextDouble() < 0.10)
            {
                _logger.LogInformation("Activity spike detected in scope {ScopeId}, triggering immediate engagement", scopeId);

                // Wait a short random delay to feel natural (2-8 seconds)
                var delay = TimeSpan.FromSeconds(new Random().Next(2, 9));
                await Task.Delay(delay);

                // Trigger engagement appropriate for high activity
                var actionChoice = new Random().Next(3) switch
                {
                    0 => 5, // IncreaseEngagement - join the conversation
                    1 => 1, // AskQuestion - ask about what's happening
                    _ => 3  // ShareFunny - add humor to the active chat
                };

                await ExecuteEngagementAction(scopeId, actionChoice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking activity spike for scope {ScopeId}", scopeId);
        }
    }

    /// <summary>
    /// Executes a specific engagement action
    /// </summary>
    private async Task<string?> ExecuteEngagementAction(ulong scopeId, int actionChoice)
    {
        try
        {
            string? response = null;

            switch (actionChoice)
            {
                case 0:
                    response = await StartTopicAsync(scopeId);
                    break;
                case 1:
                    response = await AskQuestionAsync(scopeId);
                    break;
                case 2:
                    response = await ShareInterestingContentAsync(scopeId);
                    break;
                case 3:
                    response = await ShareFunnyContentAsync(scopeId);
                    break;
                case 4:
                    response = await ShareUsefulContentAsync(scopeId);
                    break;
                case 5:
                    response = await IncreaseEngagementAsync(scopeId);
                    break;
                case 6:
                    response = await ShareOpinionAsync(scopeId);
                    break;
                case 7:
                    response = await AdaptiveResponseAsync(scopeId);
                    break;
            }

            // Clear context messages on successful engagement
            if (!string.IsNullOrEmpty(response))
            {
                ClearContextMessages(scopeId);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing engagement action {Action} for scope {ScopeId}", actionChoice, scopeId);
            return null;
        }
    }

    #endregion
}