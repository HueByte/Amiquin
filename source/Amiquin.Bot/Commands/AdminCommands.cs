using Amiquin.Bot.Commands.AutoComplete;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.Configuration;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.ModelProvider;
using Amiquin.Core.Services.Sleep;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Amiquin.Bot.Commands;

[Group("admin", "Admin commands")]
[RequireUserPermission(Discord.GuildPermission.ModerateMembers)]
public class AdminCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly ILogger<AdminCommands> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleService _toggleService;
    private readonly IChatSessionService _chatSessionService;
    private readonly BotContextAccessor _botContextAccessor;
    private readonly IConfigurationInteractionService _configurationService;
    private readonly IChatContextService _chatContextService;
    private readonly IPersonaChatService _personaChatService;
    private readonly ISleepService _sleepService;
    private readonly IModelProviderMappingService _modelProviderMappingService;

    public AdminCommands(
        ILogger<AdminCommands> logger, 
        IServerMetaService serverMetaService, 
        IToggleService toggleService, 
        IChatSessionService chatSessionService, 
        BotContextAccessor botContextAccessor,
        IConfigurationInteractionService configurationService,
        IChatContextService chatContextService,
        IPersonaChatService personaChatService,
        ISleepService sleepService,
        IModelProviderMappingService modelProviderMappingService)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _chatSessionService = chatSessionService;
        _botContextAccessor = botContextAccessor;
        _configurationService = configurationService;
        _chatContextService = chatContextService;
        _personaChatService = personaChatService;
        _sleepService = sleepService;
        _modelProviderMappingService = modelProviderMappingService;
    }

    [SlashCommand("set-persona", "Set the server persona")]
    public async Task SetServerPersonaAsync([Summary("persona", "The persona to set for the server")] string persona)
    {
        if (string.IsNullOrWhiteSpace(persona))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ You must provide a persona to set.");
            return;
        }

        var serverMeta = _botContextAccessor.ServerMeta;
        if (serverMeta is null)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found. Please try again later.");
            return;
        }

        serverMeta.Persona = persona;
        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

        var embed = new EmbedBuilder()
            .WithTitle("✅ Persona Updated")
            .WithDescription($"Server persona has been updated successfully.")
            .AddField("New Persona", persona.Length > 100 ? $"{persona[..100]}..." : persona, false)
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        _logger.LogInformation("Admin {UserId} set server persona for guild {GuildId}", Context.User.Id, Context.Guild.Id);
    }

    [SlashCommand("say", "Make the bot say something")]
    [Ephemeral]
    public async Task SayAsync([Summary("message", "The message to say")] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ You must provide a message to say.");
            return;
        }

        await Context.Channel.SendMessageAsync(message);
        await ModifyOriginalResponseAsync(msg => msg.Content = $"✅ Message sent successfully.");
    }

    [SlashCommand("embed-say", "Make the bot say something in an embed")]
    [Ephemeral]
    public async Task EmbedSayAsync(
        string title, 
        string? thumbnail, 
        [Summary("message", "The message to say")] string message, 
        bool withAuthor = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ You must provide a message to say.");
            return;
        }

        message = message.Replace("\\n", "\n").Trim();

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(message)
            .WithColor(Color.Magenta);

        if (!string.IsNullOrWhiteSpace(thumbnail))
            embedBuilder.WithThumbnailUrl(thumbnail);

        if (withAuthor)
            embedBuilder.WithAuthor(Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl());

        var embed = embedBuilder.Build();
        await Context.Channel.SendMessageAsync(embed: embed);
        await ModifyOriginalResponseAsync(msg => msg.Content = "✅ Embed sent successfully.");
    }

    [SlashCommand("toggle", "Toggle a server feature")]
    [Ephemeral]
    public async Task ToggleAsync(
        [Summary("toggle", "The feature to toggle")][Autocomplete(typeof(ToggleAutoCompleteHandler))] string toggleName,
        bool isEnabled,
        string? description = null)
    {
        await _toggleService.SetServerToggleAsync(Context.Guild.Id, toggleName, isEnabled, description);
        
        var embed = new EmbedBuilder()
            .WithTitle("⚙️ Toggle Updated")
            .WithDescription($"Feature toggle has been updated successfully.")
            .AddField("Feature", toggleName, true)
            .AddField("Status", isEnabled ? "✅ Enabled" : "❌ Disabled", true)
            .WithColor(isEnabled ? Color.Green : Color.Red)
            .WithCurrentTimestamp()
            .Build();

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        _logger.LogInformation("Admin {UserId} toggled {Toggle} to {State} for guild {GuildId}", 
            Context.User.Id, toggleName, isEnabled, Context.Guild.Id);
    }

    [SlashCommand("nuke", "Delete multiple messages from the channel")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task NukeAsync(int messageCount)
    {
        if (messageCount < Constants.Limits.MessageHistoryMinCount || messageCount > Constants.Limits.MessageHistoryMaxCount)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                $"❌ Message count must be between {Constants.Limits.MessageHistoryMinCount} and {Constants.Limits.MessageHistoryMaxCount}");
            return;
        }

        try
        {
            var messages = await Context.Channel.GetMessagesAsync(messageCount).FlattenAsync();
            var messagesToDelete = messages.Where(m => (DateTimeOffset.UtcNow - m.CreatedAt).TotalDays < 14).ToList();

            if (messagesToDelete.Any())
            {
                await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messagesToDelete);
                
                var embed = new EmbedBuilder()
                    .WithTitle("🗑️ Messages Deleted")
                    .WithDescription($"Successfully deleted {messagesToDelete.Count} messages.")
                    .WithColor(Color.Orange)
                    .WithFooter($"Requested by {Context.User.Username}")
                    .WithCurrentTimestamp()
                    .Build();

                await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
            }
            else
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ No messages found that can be deleted (messages must be less than 14 days old).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nuke command for guild {GuildId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while deleting messages.");
        }
    }

    [SlashCommand("calm-down", "Reset Amiquin's engagement rate to baseline")]
    [Ephemeral]
    public async Task CalmDownAsync()
    {
        try
        {
            var guildId = Context.Guild.Id;
            
            // Reset the engagement rate for this guild
            _chatContextService.ResetEngagement(guildId);
            
            // Get the current engagement level for display
            var currentLevel = _chatContextService.GetEngagementMultiplier(guildId);
            
            var embed = new EmbedBuilder()
                .WithTitle("😌 Calming Down...")
                .WithDescription("I'll take it easy for a bit. My engagement has been reset to baseline.")
                .AddField("Engagement Level", $"Reset to {currentLevel:F1}x (baseline)", true)
                .AddField("Context", "Cleared all conversation context", true)
                .AddField("Status", "🌙 Relaxed mode activated", false)
                .WithColor(new Color(135, 206, 235)) // Sky blue for calm
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
                .WithFooter("Use mentions to re-engage me if needed")
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
            
            _logger.LogInformation("Admin {UserId} reset engagement for guild {GuildId}", 
                Context.User.Id, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting engagement for guild {GuildId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while resetting engagement.");
        }
    }

    [SlashCommand("compact", "Manually trigger chat history optimization")]
    [Ephemeral]
    public async Task CompactAsync()
    {
        try
        {
            var guildId = Context.Guild.Id;
            
            // Trigger history optimization for this guild's chat sessions
            var (success, message) = await _personaChatService.TriggerHistoryOptimizationAsync(guildId);
            
            var embed = new EmbedBuilder()
                .WithTitle(success ? "📦 History Compacted" : "⚠️ Compaction Issue")
                .WithDescription(message)
                .WithColor(success ? new Color(46, 204, 113) : new Color(231, 76, 60)) // Green for success, red for failure
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
                .WithCurrentTimestamp();
            
            if (success)
            {
                embed.AddField("💾 Benefits", "• Reduced memory usage\n• Faster response times\n• Lower token costs", true);
                embed.AddField("🔄 What happened", "• Older messages summarized\n• Recent messages preserved\n• Context maintained", true);
                embed.WithFooter("History optimization helps maintain performance");
            }
            else
            {
                embed.WithFooter("Try again later or check if there are enough messages to compact");
            }

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
            
            _logger.LogInformation("Admin {UserId} triggered history optimization for guild {GuildId} - Success: {Success}", 
                Context.User.Id, guildId, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering history optimization for guild {GuildId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while optimizing chat history.");
        }
    }

    [SlashCommand("set-model", "Set the AI model for the server")]
    public async Task SetModelAsync(
        [Summary("model", "The AI model to use")] [Autocomplete(typeof(ModelAutoCompleteHandler))] string model)
    {
        try
        {
            var serverId = Context.Guild.Id;
            
            // Auto-detect provider from model
            var provider = _modelProviderMappingService.GetProviderForModel(model);
            if (string.IsNullOrEmpty(provider))
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown model: **{model}**. Use autocomplete to see available models.");
                return;
            }
            
            // Update server meta
            var serverMeta = _botContextAccessor.ServerMeta;
            if (serverMeta != null)
            {
                serverMeta.PreferredProvider = provider;
                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
            }

            // Update active sessions
            var updatedCount = await _chatSessionService.UpdateServerSessionModelAsync(serverId, model, provider);

            var embed = new EmbedBuilder()
                .WithTitle("✅ AI Model Updated")
                .WithDescription(updatedCount > 0 
                    ? $"Updated {updatedCount} active session(s) to use **{model}** from **{provider}**"
                    : $"Server will now use **{model}** from **{provider}** for new chat sessions")
                .AddField("Model", model, true)
                .AddField("Provider", provider, true)
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
            
            _logger.LogInformation("Admin {UserId} updated AI model for server {ServerId} to {Model} from {Provider}",
                Context.User.Id, serverId, model, provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting model {Model} for server {ServerId}", 
                model, Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the AI model.");
        }
    }

    [SlashCommand("sleep", "Put Amiquin to sleep for a specified duration")]
    public async Task AdminSleepAsync(
        [Summary("minutes", "Duration in minutes (max 1440 for 24 hours)")]
        [MinValue(1)]
        [MaxValue(1440)]
        int minutes)
    {
        try
        {
            // Check if already sleeping
            if (await _sleepService.IsSleepingAsync(Context.Guild.Id))
            {
                var remainingSleep = await _sleepService.GetRemainingSleepTimeAsync(Context.Guild.Id);
                if (remainingSleep.HasValue)
                {
                    var remainingMinutes = (int)remainingSleep.Value.TotalMinutes + 1; // Round up
                    await RespondAsync($"😴 I'm already sleeping! I'll wake up in about **{remainingMinutes} minutes**.\n" +
                                     "Use `/admin wake-up` to wake me up early.", ephemeral: true);
                    return;
                }
            }

            // Put bot to sleep
            var wakeUpTime = await _sleepService.PutToSleepAsync(Context.Guild.Id, minutes);

            var embed = new EmbedBuilder()
                .WithTitle("😴 Going to Sleep (Admin)")
                .WithDescription($"I'm going to sleep for **{minutes} minutes** as requested by an admin.")
                .WithColor(Color.DarkPurple)
                .AddField("💤 Sleep Duration", $"{minutes} minutes ({TimeSpan.FromMinutes(minutes):h\\:mm})", true)
                .AddField("⏰ Wake Up Time", $"<t:{((DateTimeOffset)wakeUpTime).ToUnixTimeSeconds()}:F>", true)
                .AddField("👮 Requested By", Context.User.Mention, true)
                .WithFooter("Use /admin wake-up to end sleep early")
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            _logger.LogInformation("Admin {UserId} put bot to sleep for {Minutes} minutes on server {ServerId}",
                Context.User.Id, minutes, Context.Guild.Id);
        }
        catch (ArgumentException ex)
        {
            await RespondAsync($"❌ {ex.Message}", ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting bot to sleep on server {ServerId}", Context.Guild.Id);
            await RespondAsync("❌ An error occurred while putting the bot to sleep.", ephemeral: true);
        }
    }

    [SlashCommand("wake-up", "Wake up Amiquin if it's sleeping")]
    public async Task WakeUpAsync()
    {
        try
        {
            var wasAwake = await _sleepService.WakeUpAsync(Context.Guild.Id);

            if (wasAwake)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("☀️ Good Morning!")
                    .WithDescription("I'm awake now! Thanks for waking me up early.")
                    .WithColor(Color.Gold)
                    .AddField("👮 Woken By", Context.User.Mention, true)
                    .WithFooter("Ready to assist!")
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: embed);

                _logger.LogInformation("Admin {UserId} woke up bot on server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            else
            {
                await RespondAsync("☀️ I'm already awake! No need to wake me up.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waking up bot on server {ServerId}", Context.Guild.Id);
            await RespondAsync("❌ An error occurred while waking up the bot.", ephemeral: true);
        }
    }

    [Group("config", "Configure server settings")]
    public class ConfigCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
    {
        private readonly ILogger<ConfigCommands> _logger;
        private readonly IServerMetaService _serverMetaService;
        private readonly IToggleService _toggleService;
        private readonly BotContextAccessor _botContextAccessor;
        private readonly IConfigurationInteractionService _configurationService;

        public ConfigCommands(
            ILogger<ConfigCommands> logger, 
            IServerMetaService serverMetaService, 
            IToggleService toggleService, 
            BotContextAccessor botContextAccessor,
            IConfigurationInteractionService configurationService)
        {
            _logger = logger;
            _serverMetaService = serverMetaService;
            _toggleService = toggleService;
            _botContextAccessor = botContextAccessor;
            _configurationService = configurationService;
        }

        [SlashCommand("setup", "Open interactive configuration interface")]
        public async Task SetupAsync()
        {
            try
            {
                var (embed, components) = await _configurationService.CreateConfigurationInterfaceAsync(
                    Context.Guild.Id, 
                    Context.Guild);

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = embed;
                    msg.Components = components;
                });

                _logger.LogInformation("Admin {UserId} opened configuration interface for server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening configuration interface for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while loading the configuration interface.");
            }
        }

        [SlashCommand("view", "View current server configuration")]
        public async Task ViewConfigAsync()
        {
            try
            {
                // Ensure server metadata exists first
                var serverMeta = await _serverMetaService.GetServerMetaAsync(Context.Guild.Id);
                if (serverMeta == null)
                {
                    // Create server metadata if it doesn't exist
                    serverMeta = await _serverMetaService.CreateServerMetaAsync(Context.Guild.Id, Context.Guild.Name);
                }
                
                // Ensure all toggles are created for this server
                await _toggleService.CreateServerTogglesIfNotExistsAsync(Context.Guild.Id);
                
                // Refresh serverMeta after toggle creation
                serverMeta = await _serverMetaService.GetServerMetaAsync(Context.Guild.Id);
                var toggles = await _toggleService.GetTogglesByServerId(Context.Guild.Id);

                var embed = new EmbedBuilder()
                    .WithTitle($"⚙️ Configuration for {Context.Guild.Name}")
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                // Server metadata
                embed.AddField("🎭 Persona", 
                    !string.IsNullOrWhiteSpace(serverMeta?.Persona) 
                        ? serverMeta.Persona.Length > 100 
                            ? $"{serverMeta.Persona[..100]}..." 
                            : serverMeta.Persona
                        : "*Not configured*", 
                    false);

                // Primary channel
                if (serverMeta?.PrimaryChannelId.HasValue == true)
                {
                    var channel = Context.Guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
                    embed.AddField("💬 Primary Channel", channel?.Mention ?? "*Channel not found*", true);
                }
                else
                {
                    embed.AddField("💬 Primary Channel", "*Not configured*", true);
                }

                // AI Provider
                embed.AddField("🤖 AI Provider", 
                    !string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider) 
                        ? serverMeta.PreferredProvider 
                        : "*Using default*", 
                    true);

                // Toggles summary
                var enabledCount = toggles.Count(t => t.IsEnabled);
                embed.AddField("🎛️ Features", $"{enabledCount}/{toggles.Count} enabled", true);

                // List all toggles with their status
                if (toggles.Any())
                {
                    var togglesList = toggles
                        .OrderBy(t => t.Name)
                        .Select(t => $"{(t.IsEnabled ? "✅" : "❌")} {FormatToggleName(t.Name)}")
                        .ToList();
                    
                    // Split into multiple fields if there are many toggles
                    var togglesPerField = 10;
                    for (int i = 0; i < togglesList.Count; i += togglesPerField)
                    {
                        var batch = togglesList.Skip(i).Take(togglesPerField);
                        var fieldName = i == 0 ? "Available Features" : "Available Features (cont.)";
                        embed.AddField(fieldName, string.Join("\n", batch), true);
                    }
                }

                await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing config for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while retrieving the configuration.");
            }
        }

        [SlashCommand("set", "Configure a specific server setting")]
        public async Task SetConfigAsync(
            [Summary("setting", "The setting to configure")][Autocomplete(typeof(AdminSettingAutoCompleteHandler))] string setting,
            [Summary("value", "The value to set")] string value)
        {
            try
            {
                // Ensure server metadata exists
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    // Try to get or create server metadata
                    serverMeta = await _serverMetaService.GetServerMetaAsync(Context.Guild.Id);
                    if (serverMeta == null)
                    {
                        serverMeta = await _serverMetaService.CreateServerMetaAsync(Context.Guild.Id, Context.Guild.Name);
                    }
                    _botContextAccessor.SetServerMeta(serverMeta);
                }

                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                switch (setting.ToLowerInvariant())
                {
                    case "persona":
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Persona cannot be empty.");
                            return;
                        }

                        serverMeta.Persona = value;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embed.WithTitle("✅ Persona Updated")
                            .WithDescription("Server persona has been updated successfully.")
                            .AddField("New Persona", value.Length > 200 ? $"{value[..200]}..." : value, false);
                        break;

                    case "primary-channel":
                    case "main-channel":
                    case "channel":
                        if (!ulong.TryParse(value, out var channelId))
                        {
                            // Try to parse channel mention
                            if (value.StartsWith("<#") && value.EndsWith(">"))
                            {
                                value = value[2..^1];
                                if (!ulong.TryParse(value, out channelId))
                                {
                                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid channel format.");
                                    return;
                                }
                            }
                            else
                            {
                                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid channel ID or mention.");
                                return;
                            }
                        }

                        var channel = Context.Guild.GetTextChannel(channelId);
                        if (channel == null)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Channel not found in this server.");
                            return;
                        }

                        serverMeta.PrimaryChannelId = channelId;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embed.WithTitle("✅ Primary Channel Updated")
                            .WithDescription($"Primary channel has been set to {channel.Mention}")
                            .AddField("Channel", channel.Mention, true)
                            .AddField("Channel ID", channelId.ToString(), true);
                        break;

                    case "provider":
                        var validProviders = new[] { "OpenAI", "Anthropic", "Gemini", "Grok" };
                        var matchedProvider = validProviders.FirstOrDefault(p =>
                            string.Equals(p, value, StringComparison.OrdinalIgnoreCase));

                        if (matchedProvider == null)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content =
                                $"❌ Invalid provider. Valid providers are: {string.Join(", ", validProviders)}");
                            return;
                        }

                        serverMeta.PreferredProvider = matchedProvider;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embed.WithTitle("✅ Provider Updated")
                            .WithDescription($"AI provider has been set to **{matchedProvider}**")
                            .AddField("Provider", matchedProvider, true);
                        break;

                    case "nsfw-channel":
                    case "nsfw_channel":
                        if (!ulong.TryParse(value, out var nsfwChannelId))
                        {
                            // Try to parse channel mention
                            if (value.StartsWith("<#") && value.EndsWith(">"))
                            {
                                value = value[2..^1];
                                if (!ulong.TryParse(value, out nsfwChannelId))
                                {
                                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid NSFW channel format.");
                                    return;
                                }
                            }
                            else
                            {
                                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid NSFW channel ID or mention.");
                                return;
                            }
                        }

                        var nsfwChannel = Context.Guild.GetTextChannel(nsfwChannelId);
                        if (nsfwChannel == null)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW channel not found in this server.");
                            return;
                        }

                        if (!nsfwChannel.IsNsfw)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ {nsfwChannel.Mention} is not marked as NSFW. Please mark it as 18+ in channel settings.");
                            return;
                        }

                        serverMeta.NsfwChannelId = nsfwChannelId;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embed.WithTitle("✅ NSFW Channel Updated")
                            .WithDescription($"NSFW channel has been set to {nsfwChannel.Mention}")
                            .AddField("Channel", nsfwChannel.Mention, true)
                            .AddField("Channel ID", nsfwChannelId.ToString(), true);
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**");
                        return;
                }

                await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
                
                _logger.LogInformation("Admin {UserId} updated setting {Setting} to {Value} for server {ServerId}",
                    Context.User.Id, setting, value, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting config {Setting} to {Value} for server {ServerId}",
                    setting, value, Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the configuration.");
            }
        }

        [SlashCommand("reset", "Reset a configuration setting to default")]
        public async Task ResetConfigAsync(
            [Summary("setting", "The setting to reset")][Autocomplete(typeof(AdminSettingAutoCompleteHandler))] string setting)
        {
            try
            {
                // Ensure server metadata exists
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    // Try to get or create server metadata
                    serverMeta = await _serverMetaService.GetServerMetaAsync(Context.Guild.Id);
                    if (serverMeta == null)
                    {
                        serverMeta = await _serverMetaService.CreateServerMetaAsync(Context.Guild.Id, Context.Guild.Name);
                    }
                    _botContextAccessor.SetServerMeta(serverMeta);
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🔄 Configuration Reset")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp();

                switch (setting.ToLowerInvariant())
                {
                    case "persona":
                        serverMeta.Persona = string.Empty;
                        embed.WithDescription("Server persona has been reset to default.");
                        break;

                    case "primary-channel":
                    case "main-channel":
                    case "channel":
                        serverMeta.PrimaryChannelId = null;
                        embed.WithDescription("Primary channel has been reset.");
                        break;

                    case "provider":
                        serverMeta.PreferredProvider = null;
                        embed.WithDescription("AI provider has been reset to default.");
                        break;

                    case "nsfw-channel":
                    case "nsfw_channel":
                        serverMeta.NsfwChannelId = null;
                        embed.WithDescription("NSFW channel has been reset.");
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**");
                        return;
                }

                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
                
                _logger.LogInformation("Admin {UserId} reset setting {Setting} for server {ServerId}",
                    Context.User.Id, setting, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting config {Setting} for server {ServerId}", 
                    setting, Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while resetting the configuration.");
            }
        }

        [SlashCommand("export", "Export server configuration")]
        public async Task ExportConfigAsync()
        {
            try
            {
                var serverMeta = _botContextAccessor.ServerMeta;
                var toggles = await _toggleService.GetTogglesByServerId(Context.Guild.Id);

                var export = new StringBuilder();
                export.AppendLine($"# Server Configuration Export");
                export.AppendLine($"Server: {Context.Guild.Name}");
                export.AppendLine($"Server ID: {Context.Guild.Id}");
                export.AppendLine($"Export Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                export.AppendLine();
                
                export.AppendLine("## Server Settings");
                export.AppendLine($"- Persona: {serverMeta?.Persona ?? "Not configured"}");
                export.AppendLine($"- Primary Channel ID: {serverMeta?.PrimaryChannelId?.ToString() ?? "Not configured"}");
                export.AppendLine($"- NSFW Channel ID: {serverMeta?.NsfwChannelId?.ToString() ?? "Not configured"}");
                export.AppendLine($"- Preferred Provider: {serverMeta?.PreferredProvider ?? "Default"}");
                export.AppendLine();
                
                export.AppendLine("## Feature Toggles");
                foreach (var toggle in toggles.OrderBy(t => t.Name))
                {
                    export.AppendLine($"- {toggle.Name}: {(toggle.IsEnabled ? "Enabled" : "Disabled")}");
                    if (!string.IsNullOrWhiteSpace(toggle.Description))
                        export.AppendLine($"  Description: {toggle.Description}");
                }

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(export.ToString()));
                await Context.Channel.SendFileAsync(
                    stream, 
                    $"config_{Context.Guild.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt",
                    $"Configuration export for **{Context.Guild.Name}**");
                
                await ModifyOriginalResponseAsync(msg => msg.Content = "✅ Configuration exported successfully!");
                
                _logger.LogInformation("Admin {UserId} exported configuration for server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting config for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while exporting the configuration.");
            }
        }
        
        /// <summary>
        /// Formats toggle names to be more user-friendly by adding spaces between words.
        /// </summary>
        private static string FormatToggleName(string toggleName)
        {
            // Convert from PascalCase to readable format
            // e.g., "EnableChat" -> "Enable Chat"
            return System.Text.RegularExpressions.Regex.Replace(
                toggleName, 
                "([a-z])([A-Z])", 
                "$1 $2"
            );
        }
    }
}