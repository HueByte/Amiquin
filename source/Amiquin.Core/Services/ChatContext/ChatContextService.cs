using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Meta;
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
    private readonly IChatCoreService _chatCoreService;
    private readonly IServerMetaService _serverMetaService;
    public readonly ConcurrentDictionary<ulong, ConcurrentBag<SocketMessage>> Messages = new();

    // Track engagement multipliers per guild (higher = more engagement)
    private readonly ConcurrentDictionary<ulong, float> _engagementMultipliers = new();

    public ChatContextService(ILogger<ChatContextService> logger, IServiceScopeFactory serviceScopeFactory, IChatCoreService chatCoreService, IServerMetaService serverMetaService)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _chatCoreService = chatCoreService;
        _serverMetaService = serverMetaService;
    }

    public Task HandleUserMessageAsync(ulong scopeId, SocketMessage socketMessage)
    {
        var message = socketMessage.Content.Trim();
        if (socketMessage.Author.IsBot || string.IsNullOrWhiteSpace(message))
        {
            _logger.LogInformation("Ignoring message from bot or empty message in scope {ScopeId}", scopeId);
            return Task.CompletedTask;
        }

        var username = socketMessage.Author.GlobalName ?? socketMessage.Author.Username;
        _logger.LogInformation("Received message from {Username} in scope {ScopeId}: {Message}", username, scopeId, message);

        // Check if bot was mentioned and increase engagement
        CheckForBotMentionAndIncreaseEngagement(scopeId, socketMessage);

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
        try
        {
            if (Messages.TryGetValue(scopeId, out var messages))
            {
                return messages.Select(m => m.Content.Trim()).ToArray();
            }

            _logger.LogWarning("No messages found for scope {ScopeId}", scopeId);
            return Array.Empty<string>();
        }
        finally
        {
            Messages.TryRemove(scopeId, out _);
            _logger.LogInformation("Cleared messages for scope {ScopeId}", scopeId);
        }
    }

    #region Actions to engage with users
    public async Task<string?> StartTopicAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Starting random topic for guild {GuildId}", guildId);

            var prompt = "Provide a fun, interesting, or thought-provoking conversation starter as Amiquin. " +
                        "Keep it casual and engaging, something that would naturally start a discussion in a Discord server. " +
                        "Don't use quotation marks. Just provide the message directly.";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Started topic in guild {GuildId}: {Topic}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
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
        try
        {
            _logger.LogInformation("Answering mention in guild {GuildId} from {Username}",
                guildId, mentionMessage.Author.Username);

            var context = FormatContextMessagesForAI(guildId);
            var username = mentionMessage.Author.GlobalName ?? mentionMessage.Author.Username;
            var mentionContent = mentionMessage.Content.Trim();

            var prompt = $"You were mentioned by {username} who said: '{mentionContent}'. " +
                        $"Respond naturally as Amiquin. Here's recent context if helpful:\n{context}";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = channel ?? mentionMessage.Channel as IMessageChannel;
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Answered mention in guild {GuildId}: {Response}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering mention for guild {GuildId}", guildId);
            return null;
        }
    }

    public async Task<string?> AskQuestionAsync(ulong guildId, IMessageChannel? channel = null)
    {
        try
        {
            _logger.LogInformation("Asking engaging question for guild {GuildId}", guildId);

            var context = FormatContextMessagesForAI(guildId);
            var basePrompt = "Ask an engaging question as Amiquin that would spark discussion. " +
                           "Make it fun, interesting, or thought-provoking. Don't use quotation marks.";

            var prompt = !string.IsNullOrWhiteSpace(context)
                ? $"{basePrompt} Here's recent conversation context for relevance:\n{context}"
                : basePrompt;

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Asked question in guild {GuildId}: {Question}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
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

            var prompt = "Share something interesting, educational, or thought-provoking as Amiquin. " +
                        "It could be a fun fact, an interesting observation about technology/gaming/life, " +
                        "or something that would spark curiosity. Keep it engaging and conversational. " +
                        "Don't use quotation marks.";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Shared interesting content in guild {GuildId}: {Content}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
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

            var prompt = "Provide a funny message, joke, or humorous observation as Amiquin. " +
                        "Keep it light-hearted, appropriate for Discord, and entertaining. " +
                        "It could be about gaming, tech, everyday life, or just a clever observation. " +
                        "Don't use quotation marks.";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                var targetChannel = await GetTargetChannelAsync(guildId, channel);
                if (targetChannel != null)
                {
                    await targetChannel.SendMessageAsync(response);
                    _logger.LogInformation("Shared funny content in guild {GuildId}: {Content}",
                        guildId, response.Substring(0, Math.Min(response.Length, 100)) + "...");
                }
                return response;
            }

            return null;
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

            var prompt = "Share useful tips, advice, or helpful information as Amiquin. " +
                        "It could be about productivity, gaming tips, tech advice, Discord features, " +
                        "or general life hacks. Make it practical and valuable. " +
                        "Don't use quotation marks.";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

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

            var prompt = "Share interesting tech news, gaming updates, or general interesting developments as Amiquin. " +
                        "Keep it conversational and engaging, focusing on things that would interest a Discord community. " +
                        "Don't make up specific facts - keep it general and discussion-oriented. " +
                        "Don't use quotation marks.";

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

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

            var context = FormatContextMessagesForAI(guildId);
            var basePrompt = "Create an engaging message as Amiquin to spark activity and conversation. " +
                           "This could be asking for opinions, starting a mini-game, creating a poll idea, " +
                           "or encouraging community interaction. Make it fun and inviting. " +
                           "Don't use quotation marks.";

            var prompt = !string.IsNullOrWhiteSpace(context)
                ? $"{basePrompt} Here's recent context for relevance:\n{context}"
                : basePrompt;

            var response = await _chatCoreService.ExchangeMessageAsync(prompt);

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

    #endregion

    #region Private Helper Methods

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
                var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
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

            // Fall back to default text channel (first text channel the bot can send messages to)
            var defaultChannel = guild.TextChannels
                .Where(c => c.GetPermissionOverwrite(discordClient.CurrentUser)?.SendMessages != PermValue.Deny)
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
    /// Formats context messages in the same format as /chat command: [username]: message
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
                var content = msg.Content.Trim();

                if (!string.IsNullOrWhiteSpace(content))
                {
                    contextLines.Add($"[{username}]: {content}");
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
            var prompt = $"Based on the recent conversation context below, engage naturally as if you're part of the discussion. " +
                        $"Don't announce that you're responding to context. Just participate naturally in the ongoing conversation:\n\n{contextMessages}";

            // Get AI response using the persona chat service
            var response = await personaChatService.ExchangeMessageAsync(guild.Id, prompt);

            if (!string.IsNullOrWhiteSpace(response))
            {
                // Send the message to the channel
                await channel.SendMessageAsync(response);
                _logger.LogInformation("Sent context-aware message to {ChannelName} in {GuildName}: {Response}",
                    channel.Name, guild.Name, response.Substring(0, Math.Min(response.Length, 100)) + "...");

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

    #endregion
}