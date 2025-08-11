using Amiquin.Bot.Commands.AutoComplete;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
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
    private readonly IPaginationService _paginationService;
    private readonly BotContextAccessor _botContextAccessor;
    private const int WORKER_COUNT = 1;
    private const int DELETE_THROTTLE_MS = 500;
    private const int UPDATE_THROTTLE_MS = 1500;
    private const int BAR_WIDTH = 40;

    public AdminCommands(ILogger<AdminCommands> logger, IServerMetaService serverMetaService, IToggleService toggleService, IChatSessionService chatSessionService, IPaginationService paginationService, BotContextAccessor botContextAccessor)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _chatSessionService = chatSessionService;
        _paginationService = paginationService;
        _botContextAccessor = botContextAccessor;
    }

    [SlashCommand("set-server-persona", "Set the server persona")]
    public async Task SetServerPersonaAsync([Summary("persona", "The persona to set for the server")] string persona)
    {
        if (string.IsNullOrWhiteSpace(persona))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a persona to set.");
            return;
        }

        var serverMeta = _botContextAccessor.ServerMeta;
        if (serverMeta is null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "Server meta not found. Please try again later.");
            return;
        }

        serverMeta.Persona = persona;

        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set server persona to: {persona}");
    }

    [SlashCommand("say", "Make the bot say something")]
    [Ephemeral]
    public async Task SayAsync([Summary("message", "The message to say")] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a message to say.");
            return;
        }

        var response = await Context.Channel.SendMessageAsync(message);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Said: {response.Content}");
    }

    [SlashCommand("embed-say", "Make the bot say something")]
    [Ephemeral]
    public async Task EmbedSayAsync(string title, string thumbnail, [Summary("message", "The message to say")] string message, bool withAuthor = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a message to say.");
            return;
        }

        message = message.Replace("\\n", "\n").Trim(); // Add blockquote formatting for new lines

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithThumbnailUrl(thumbnail)
            .WithDescription(message)
            .WithColor(Color.Magenta);

        if (withAuthor)
            embedBuilder.WithAuthor(Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl(), Context.User.GetAvatarUrl());

        var embed = embedBuilder.Build();

        var response = await Context.Channel.SendMessageAsync(embed: embed);
        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }

    [SlashCommand("server-toggles", "List all server toggles")]
    [Ephemeral]
    public async Task ServerTogglesAsync()
    {
        var toggles = await _toggleService.GetTogglesByServerId(Context.Guild.Id);
        var sb = new StringBuilder();

        sb.AppendLine("```ini");
        foreach (var toggle in toggles)
        {
            sb.AppendLine($"{toggle.Name} = {toggle.IsEnabled}");
        }
        sb.AppendLine("```");

        var embed = new EmbedBuilder()
            .WithTitle("Server Toggles")
            .WithThumbnailUrl(Context.Guild.IconUrl)
            .WithDescription(sb.ToString())
            .WithColor(Color.DarkTeal)
            .Build();

        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }

    [SlashCommand("toggle", "Toggle a feature")]
    [Ephemeral]
    public async Task ToggleAsync(
        [Summary("toggle", "The feature to toggle")][Autocomplete(typeof(ToggleAutoCompleteHandler))] string toggleName, 
        bool isEnabled, 
        string? description = null)
    {
        await _toggleService.SetServerToggleAsync(Context.Guild.Id, toggleName, isEnabled, description);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set {toggleName} to {isEnabled}");
    }

    [SlashCommand("nuke", "Nuke the channel")]
    public async Task NukeAsync(int messageCount)
    {
        IUserMessage currentMessage = await ModifyOriginalResponseAsync((msg) => msg.Content = "Preparing to nuke...");
        if (messageCount < Constants.Limits.MessageHistoryMinCount || messageCount > Constants.Limits.MessageHistoryMaxCount)
        {
            await currentMessage.ModifyAsync((msg) => msg.Content = $"Message count must be between {Constants.Limits.MessageHistoryMinCount} and {Constants.Limits.MessageHistoryMaxCount}");
            return;
        }

        // +1 as we need to include the self message
        var messageBatches = await Context.Channel.GetMessagesAsync(messageCount + 1, CacheMode.AllowDownload, RequestOptions.Default).ToListAsync();

        var messages = messageBatches.SelectMany(x => x).ToList();

        _logger.LogInformation("{iteractionId} | Deleting {MessageCount} messages in [{ChannelId}]", Context.Interaction.Id, messages.Count - 1, Context.Channel.Id);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Deleting {messages.Count - 1} messages");

        ConcurrentBag<Discord.IMessage> messagesBag = [.. messages];
        var workers = Enumerable.Range(0, WORKER_COUNT).Select(async _ =>
        {
            while (messagesBag.TryTake(out var message))
            {
                // Throttle the deletion to avoid rate limiting 
                await Task.Delay(DELETE_THROTTLE_MS);

                if (message.Id == currentMessage.Id)
                {
                    _logger.LogInformation("{iteractionId} | Skipping self message {MessageId} in [{ChannelId}]", Context.Interaction.Id, message.Id, Context.Channel.Id);
                    continue;
                }

                await DeleteMessageAsync(message);
            }
        }).ToList();

        var updater = Task.Run(async () =>
        {
            double previousProgress = 0;
            while (messagesBag.Count > 0)
            {
                var progress = ProgressUtilities.GetCompletionPercentage(messageCount - messagesBag.Count, messageCount);
                if (progress == previousProgress)
                {
                    await Task.Delay(UPDATE_THROTTLE_MS);
                    continue;
                }

                previousProgress = progress;


                var consoleProgressBar = ProgressUtilities.GenerateConsoleProgressBar(progress, BAR_WIDTH);
                var discordProgressBar = ProgressUtilities.GenerateNachoProgressBar(progress, BAR_WIDTH);

                _logger.LogInformation("Nuke progress: {ProgressPercent}% in [{channelId}]\n{bar}", (int)(progress * 100), Context.Channel.Id, consoleProgressBar);

                var calculatedMessage = $"In progress {(int)(progress * 100)}% {Constants.Emoji.SlugParty} *({messageCount - messagesBag.Count - 1}/{messageCount})* \n{discordProgressBar}";
                await currentMessage.ModifyAsync((msg) => msg.Content = calculatedMessage);

                await Task.Delay(UPDATE_THROTTLE_MS);
            }
        });

        await Task.WhenAll(workers);
        await updater;

        await currentMessage.ModifyAsync((msg) => msg.Content = $"Nuke finished | {messages.Count - 1} messages deleted\n[*No nachos were harmed in the making of this nuke*]");
    }

    private async Task DeleteMessageAsync(Discord.IMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message {MessageId}", message.Id);
        }
    }


    [SlashCommand("current-model", "Show which AI model is currently being used")]
    public async Task ShowCurrentModelAsync()
    {
        try
        {
            var serverId = Context.Guild.Id;
            var activeSession = await _chatSessionService.GetActiveServerSessionAsync(serverId);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("🤖 Current AI Model Configuration")
                .WithColor(Color.Green)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithCurrentTimestamp();

            if (activeSession != null)
            {
                embedBuilder.AddField("Current Model", activeSession.Model, inline: true);
                embedBuilder.AddField("Current Provider", activeSession.Provider, inline: true);
                embedBuilder.AddField("Session Created", activeSession.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), inline: true);
                embedBuilder.AddField("Last Activity", activeSession.LastActivityAt.ToString("yyyy-MM-dd HH:mm:ss UTC"), inline: true);
                embedBuilder.AddField("Messages Count", activeSession.MessageCount.ToString(), inline: true);
                embedBuilder.AddField("Estimated Tokens", activeSession.EstimatedTokens.ToString(), inline: true);

                embedBuilder.WithDescription($"Server **{Context.Guild.Name}** is currently using **{activeSession.Model}** from **{activeSession.Provider}**");
            }
            else
            {
                embedBuilder.WithDescription("No active chat session found for this server. A new session will be created when the first chat command is used.");
                embedBuilder.AddField("Default Model", "gpt-4o-mini", inline: true);
                embedBuilder.AddField("Default Provider", "OpenAI", inline: true);
            }

            // Show available models
            var availableModels = await _chatSessionService.GetAvailableModelsAsync();
            if (availableModels.Any())
            {
                var modelsText = new StringBuilder();
                foreach (var provider in availableModels)
                {
                    modelsText.AppendLine($"**{provider.Key}**: {string.Join(", ", provider.Value)}");
                }
                embedBuilder.AddField("Available Models", modelsText.ToString().Trim(), inline: false);
            }

            await ModifyOriginalResponseAsync(msg => msg.Embed = embedBuilder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current model for server {ServerId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while retrieving current model information.");
        }
    }

    [SlashCommand("set-model", "Set the AI model for this server")]
    public async Task SetModelAsync(
        [Summary("model", "The AI model to use")][Autocomplete(typeof(ModelAutoCompleteHandler))] string model,
        [Summary("provider", "The AI provider")][Autocomplete(typeof(ProviderAutoCompleteHandler))] string provider)
    {
        try
        {
            // Validate the model and provider combination
            var isValid = await _chatSessionService.ValidateModelProviderAsync(model, provider);
            if (!isValid)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Invalid model/provider combination: **{model}** from **{provider}**. Use `/admin current-model` to see available options.");
                return;
            }

            var serverId = Context.Guild.Id;
            var updatedCount = await _chatSessionService.UpdateServerSessionModelAsync(serverId, model, provider);
            
            // Also update the server's preferred provider in ServerMeta
            var serverMeta = await _serverMetaService.GetServerMetaAsync(serverId);
            if (serverMeta != null)
            {
                serverMeta.PreferredProvider = provider;
                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                _logger.LogInformation("Updated server {ServerId} preferred provider to {Provider}", serverId, provider);
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle("✅ AI Model Updated Successfully")
                .WithColor(Color.Green)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithCurrentTimestamp();

            if (updatedCount > 0)
            {
                embedBuilder.WithDescription($"Updated **{updatedCount}** active session(s) for server **{Context.Guild.Name}** to use **{model}** from **{provider}**");
            }
            else
            {
                embedBuilder.WithDescription($"Server **{Context.Guild.Name}** will now use **{model}** from **{provider}** for new chat sessions");
            }

            embedBuilder.AddField("New Model", model, inline: true);
            embedBuilder.AddField("New Provider", provider, inline: true);
            embedBuilder.AddField("Server", Context.Guild.Name, inline: true);

            await ModifyOriginalResponseAsync(msg => msg.Embed = embedBuilder.Build());

            _logger.LogInformation("Admin {UserId} updated AI model for server {ServerId} to {Model} from {Provider}",
                Context.User.Id, serverId, model, provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting model {Model} from {Provider} for server {ServerId}", model, provider, Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the AI model.");
        }
    }

    [Group("config", "Configure server settings")]
    public class ConfigCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
    {
        private readonly ILogger<ConfigCommands> _logger;
        private readonly IServerMetaService _serverMetaService;
        private readonly IChatSessionService _chatSessionService;
        private readonly IToggleService _toggleService;
        private readonly IPaginationService _paginationService;
        private readonly BotContextAccessor _botContextAccessor;

        public ConfigCommands(ILogger<ConfigCommands> logger, IServerMetaService serverMetaService, IChatSessionService chatSessionService, IToggleService toggleService, IPaginationService paginationService, BotContextAccessor botContextAccessor)
        {
            _logger = logger;
            _serverMetaService = serverMetaService;
            _chatSessionService = chatSessionService;
            _toggleService = toggleService;
            _paginationService = paginationService;
            _botContextAccessor = botContextAccessor;
        }

        [SlashCommand("set", "Configure a server setting")]
        public async Task SetConfigAsync(
            [Summary("setting", "The setting to configure")][Autocomplete(typeof(AdminSettingAutoCompleteHandler))] string setting,
            [Summary("value", "The value to set")] string value)
        {
            try
            {
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found. Please try again later.");
                    return;
                }

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("⚙️ Server Configuration Updated")
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                switch (setting.ToLowerInvariant())
                {
                    case "main-channel":
                    case "primary-channel":
                        if (!ulong.TryParse(value, out var channelId))
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid channel ID. Please provide a valid channel ID or use the autocomplete.");
                            return;
                        }

                        var channel = Context.Guild.GetTextChannel(channelId);
                        if (channel == null)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Channel not found in this server.");
                            return;
                        }

                        serverMeta.PrimaryChannelId = channelId;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embedBuilder.WithDescription($"Primary channel has been set to {channel.Mention}");
                        embedBuilder.AddField("Setting", "Primary Channel", inline: true);
                        embedBuilder.AddField("New Value", channel.Mention, inline: true);
                        embedBuilder.AddField("Channel ID", channelId.ToString(), inline: true);
                        break;

                    case "persona":
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Persona cannot be empty.");
                            return;
                        }

                        serverMeta.Persona = value;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        var personaPreview = value.Length > 100 ? $"{value[..100]}..." : value;
                        embedBuilder.WithDescription("Server persona has been updated");
                        embedBuilder.AddField("Setting", "Server Persona", inline: false);
                        embedBuilder.AddField("New Persona", personaPreview, inline: false);
                        break;

                    case "provider":
                        var validProviders = new[] { "OpenAI", "Gemini", "Grok" };
                        var normalizedProvider = value.Trim();
                        
                        // Case-insensitive matching
                        var matchedProvider = validProviders.FirstOrDefault(p => 
                            string.Equals(p, normalizedProvider, StringComparison.OrdinalIgnoreCase));
                        
                        if (matchedProvider == null)
                        {
                            await ModifyOriginalResponseAsync(msg => msg.Content = 
                                $"❌ Invalid provider: **{value}**. Valid providers are: {string.Join(", ", validProviders)}");
                            return;
                        }

                        serverMeta.PreferredProvider = matchedProvider;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        embedBuilder.WithDescription("Server preferred provider has been updated");
                        embedBuilder.AddField("Setting", "Preferred Provider", inline: true);
                        embedBuilder.AddField("New Provider", matchedProvider, inline: true);
                        embedBuilder.AddField("Status", "✅ Active", inline: true);
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**. Use autocomplete to see available settings.");
                        return;
                }

                await ModifyOriginalResponseAsync(msg => msg.Embed = embedBuilder.Build());
                _logger.LogInformation("Admin {UserId} updated server setting {Setting} to {Value} for server {ServerId}",
                    Context.User.Id, setting, value, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting config {Setting} to {Value} for server {ServerId}", 
                    setting, value, Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the configuration.");
            }
        }

        [SlashCommand("set-channel", "Configure a specific channel setting")]
        public async Task SetChannelConfigAsync(
            [Summary("type", "The type of channel to configure")][Autocomplete(typeof(AdminSettingAutoCompleteHandler))] string type,
            [Summary("channel", "The channel to set")][Autocomplete(typeof(ChannelAutoCompleteHandler))] string channelIdStr)
        {
            try
            {
                if (!ulong.TryParse(channelIdStr, out var channelId))
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid channel ID format.");
                    return;
                }

                var channel = Context.Guild.GetTextChannel(channelId);
                if (channel == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Channel not found in this server.");
                    return;
                }

                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found. Please try again later.");
                    return;
                }

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("⚙️ Channel Configuration Updated")
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                switch (type.ToLowerInvariant())
                {
                    case "main-channel":
                    case "primary-channel":
                        serverMeta.PrimaryChannelId = channelId;
                        embedBuilder.WithDescription($"Primary channel has been set to {channel.Mention}");
                        embedBuilder.AddField("Channel Type", "Primary Channel", inline: true);
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown channel type: **{type}**");
                        return;
                }

                await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                embedBuilder.AddField("Channel", channel.Mention, inline: true);
                embedBuilder.AddField("Channel Name", $"#{channel.Name}", inline: true);
                embedBuilder.AddField("Category", channel.Category?.Name ?? "No Category", inline: true);
                embedBuilder.AddField("Channel ID", channelId.ToString(), inline: true);

                await ModifyOriginalResponseAsync(msg => msg.Embed = embedBuilder.Build());
                _logger.LogInformation("Admin {UserId} set {ChannelType} to {ChannelId} for server {ServerId}",
                    Context.User.Id, type, channelId, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting channel config for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the channel configuration.");
            }
        }

        [SlashCommand("view", "View current server configuration")]
        public async Task ViewConfigAsync()
        {
            try
            {
                var serverMeta = _botContextAccessor.ServerMeta;
                var serverId = Context.Guild.Id;

                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found.");
                    return;
                }

                // Create multiple embeds for pagination
                var embeds = new List<Embed>();

                // Page 1: Basic Server Information
                var basicInfoEmbed = new EmbedBuilder()
                    .WithTitle($"⚙️ Server Configuration - {Context.Guild.Name}")
                    .WithDescription("📋 **Basic Information & Settings**")
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                basicInfoEmbed.AddField("Server ID", serverId.ToString(), inline: true);
                basicInfoEmbed.AddField("Server Name", Context.Guild.Name, inline: true);
                basicInfoEmbed.AddField("Member Count", Context.Guild.MemberCount.ToString(), inline: true);
                basicInfoEmbed.AddField("Created", serverMeta.CreatedAt.ToString("yyyy-MM-dd HH:mm UTC"), inline: true);
                basicInfoEmbed.AddField("Last Updated", serverMeta.LastUpdated.ToString("yyyy-MM-dd HH:mm UTC"), inline: true);
                basicInfoEmbed.AddField("Active", serverMeta.IsActive ? "✅ Yes" : "❌ No", inline: true);

                // Primary Channel
                if (serverMeta.PrimaryChannelId.HasValue)
                {
                    var channel = Context.Guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
                    if (channel != null)
                    {
                        basicInfoEmbed.AddField("Primary Channel", channel.Mention, inline: true);
                    }
                    else
                    {
                        basicInfoEmbed.AddField("Primary Channel", "⚠️ Channel not found (deleted?)", inline: true);
                    }
                }
                else
                {
                    basicInfoEmbed.AddField("Primary Channel", "❌ Not configured", inline: true);
                }

                basicInfoEmbed.WithFooter("Page 1 of 3 • Use navigation buttons below");
                embeds.Add(basicInfoEmbed.Build());

                // Page 2: AI & Chat Configuration
                var aiConfigEmbed = new EmbedBuilder()
                    .WithTitle($"🤖 AI Configuration - {Context.Guild.Name}")
                    .WithDescription("🧠 **AI Model & Chat Settings**")
                    .WithColor(Color.Green)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                // Current AI Model
                var activeSession = await _chatSessionService.GetActiveServerSessionAsync(serverId);
                if (activeSession != null)
                {
                    aiConfigEmbed.AddField("Current AI Model", activeSession.Model, inline: true);
                    aiConfigEmbed.AddField("Current Provider", activeSession.Provider, inline: true);
                    aiConfigEmbed.AddField("Session Active Since", activeSession.LastActivityAt.ToString("yyyy-MM-dd HH:mm UTC"), inline: true);
                }
                else
                {
                    aiConfigEmbed.AddField("Current AI Model", "❌ No active session", inline: true);
                    aiConfigEmbed.AddField("Current Provider", "Not available", inline: true);
                    aiConfigEmbed.AddField("Session Status", "Inactive", inline: true);
                }

                // Preferred Provider
                if (!string.IsNullOrWhiteSpace(serverMeta.PreferredProvider))
                {
                    aiConfigEmbed.AddField("Preferred Provider", serverMeta.PreferredProvider, inline: true);
                }
                else
                {
                    aiConfigEmbed.AddField("Preferred Provider", "Using global default", inline: true);
                }

                // Persona information
                if (!string.IsNullOrWhiteSpace(serverMeta.Persona))
                {
                    var personaText = serverMeta.Persona.Length > 1000
                        ? $"{serverMeta.Persona[..996]}..."
                        : serverMeta.Persona;
                    aiConfigEmbed.AddField("Server Persona", personaText, inline: false);
                }
                else
                {
                    aiConfigEmbed.AddField("Server Persona", "❌ No custom persona configured", inline: false);
                }

                aiConfigEmbed.WithFooter("Page 2 of 3 • Use navigation buttons below");
                embeds.Add(aiConfigEmbed.Build());

                // Page 3: Feature Toggles
                var togglesEmbed = new EmbedBuilder()
                    .WithTitle($"🎛️ Feature Toggles - {Context.Guild.Name}")
                    .WithDescription("🔧 **Feature Flags & Settings**")
                    .WithColor(Color.Orange)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                var serverToggles = await _toggleService.GetTogglesByServerId(serverId);
                if (serverToggles.Any())
                {
                    var togglesText = new StringBuilder();
                    foreach (var toggle in serverToggles.OrderBy(t => t.Name))
                    {
                        var status = toggle.IsEnabled ? "✅" : "❌";
                        var description = toggle.IsEnabled ? "Enabled" : "Disabled";
                        togglesText.AppendLine($"{status} **{toggle.Name}** - {description}");
                    }
                    
                    togglesEmbed.AddField("Current Feature States", togglesText.ToString().Trim(), inline: false);
                }
                else
                {
                    togglesEmbed.AddField("Feature Toggles", "❌ No toggles configured", inline: false);
                }

                togglesEmbed.AddField("💡 Tip", "Use `/admin toggle` to enable/disable features", inline: false);
                togglesEmbed.WithFooter("Page 3 of 3 • Use navigation buttons below");
                embeds.Add(togglesEmbed.Build());

                // Create paginated message
                var (embed, component) = await _paginationService.CreatePaginatedMessageAsync(
                    embeds, Context.User.Id, TimeSpan.FromMinutes(10));

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Embed = embed;
                    msg.Components = component;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing config for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while retrieving the configuration.");
            }
        }

        [SlashCommand("reset", "Reset a specific configuration setting")]
        public async Task ResetConfigAsync(
            [Summary("setting", "The setting to reset")][Autocomplete(typeof(AdminSettingAutoCompleteHandler))] string setting)
        {
            try
            {
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found.");
                    return;
                }

                var embedBuilder = new EmbedBuilder()
                    .WithTitle("🔄 Configuration Reset")
                    .WithColor(Color.Orange)
                    .WithThumbnailUrl(Context.Guild.IconUrl)
                    .WithCurrentTimestamp();

                switch (setting.ToLowerInvariant())
                {
                    case "main-channel":
                    case "primary-channel":
                        serverMeta.PrimaryChannelId = null;
                        embedBuilder.WithDescription("Primary channel configuration has been reset");
                        embedBuilder.AddField("Setting Reset", "Primary Channel", inline: true);
                        break;

                    case "persona":
                        serverMeta.Persona = string.Empty;
                        embedBuilder.WithDescription("Server persona has been reset to default");
                        embedBuilder.AddField("Setting Reset", "Server Persona", inline: true);
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**");
                        return;
                }

                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                await ModifyOriginalResponseAsync(msg => msg.Embed = embedBuilder.Build());

                _logger.LogInformation("Admin {UserId} reset {Setting} for server {ServerId}",
                    Context.User.Id, setting, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting config {Setting} for server {ServerId}", setting, Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while resetting the configuration.");
            }
        }

        [SlashCommand("setup", "Interactive configuration setup using Discord modals")]
        public async Task InteractiveSetupAsync()
        {
            try
            {
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found.");
                    return;
                }

                // Create modal with configuration fields
                var modal = new ModalBuilder()
                    .WithTitle($"⚙️ Configure {Context.Guild.Name}")
                    .WithCustomId($"admin_config_modal_{Context.User.Id}_{Context.Guild.Id}")
                    .AddTextInput(
                        label: "Server Persona",
                        customId: "persona_input",
                        placeholder: "Enter a custom persona for the AI assistant...",
                        value: string.IsNullOrWhiteSpace(serverMeta.Persona) ? "" : serverMeta.Persona[..Math.Min(serverMeta.Persona.Length, 4000)],
                        style: TextInputStyle.Paragraph,
                        maxLength: 4000,
                        required: false)
                    .AddTextInput(
                        label: "Primary Channel ID",
                        customId: "primary_channel_input",
                        placeholder: "Enter the channel ID for primary bot activity",
                        value: serverMeta.PrimaryChannelId?.ToString() ?? "",
                        style: TextInputStyle.Short,
                        maxLength: 20,
                        required: false)
                    .AddTextInput(
                        label: "Preferred AI Provider",
                        customId: "preferred_provider_input",
                        placeholder: "OpenAI, Anthropic, Grok, Gemini, or leave empty for default",
                        value: serverMeta.PreferredProvider ?? "",
                        style: TextInputStyle.Short,
                        maxLength: 50,
                        required: false);

                await RespondWithModalAsync(modal.Build());
                _logger.LogInformation("Admin {UserId} opened interactive setup modal for server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening interactive setup modal for server {ServerId}", Context.Guild.Id);
                await RespondAsync("❌ An error occurred while opening the configuration modal.", ephemeral: true);
            }
        }
    }

    /// <summary>
    /// Handles modal submit interactions for the interactive configuration setup
    /// </summary>
    [ModalInteraction("admin_config_modal_*_*")]
    public async Task HandleConfigModalAsync(string userId, string guildId, InteractiveConfigModal modal)
    {
        try
        {
            // Verify the user who submitted the modal is the same who opened it
            if (!ulong.TryParse(userId, out var expectedUserId) || expectedUserId != Context.User.Id)
            {
                await RespondAsync("❌ You are not authorized to submit this configuration.", ephemeral: true);
                return;
            }

            if (!ulong.TryParse(guildId, out var expectedGuildId) || expectedGuildId != Context.Guild.Id)
            {
                await RespondAsync("❌ Invalid guild context.", ephemeral: true);
                return;
            }

            var serverMeta = _botContextAccessor.ServerMeta;
            if (serverMeta == null)
            {
                await RespondAsync("❌ Server metadata not found.", ephemeral: true);
                return;
            }

            var changes = new List<string>();

            // Process persona changes
            if (!string.IsNullOrWhiteSpace(modal.Persona) && modal.Persona != serverMeta.Persona)
            {
                serverMeta.Persona = modal.Persona.Trim();
                changes.Add("✅ **Server Persona** updated");
            }
            else if (string.IsNullOrWhiteSpace(modal.Persona) && !string.IsNullOrWhiteSpace(serverMeta.Persona))
            {
                serverMeta.Persona = string.Empty;
                changes.Add("🔄 **Server Persona** cleared");
            }

            // Process primary channel changes
            if (!string.IsNullOrWhiteSpace(modal.PrimaryChannelId))
            {
                if (ulong.TryParse(modal.PrimaryChannelId.Trim(), out var channelId))
                {
                    var channel = Context.Guild.GetTextChannel(channelId);
                    if (channel != null)
                    {
                        if (serverMeta.PrimaryChannelId != channelId)
                        {
                            serverMeta.PrimaryChannelId = channelId;
                            changes.Add($"✅ **Primary Channel** set to {channel.Mention}");
                        }
                    }
                    else
                    {
                        changes.Add("❌ **Primary Channel** - Channel not found in this server");
                    }
                }
                else
                {
                    changes.Add("❌ **Primary Channel** - Invalid channel ID format");
                }
            }
            else if (serverMeta.PrimaryChannelId.HasValue)
            {
                serverMeta.PrimaryChannelId = null;
                changes.Add("🔄 **Primary Channel** cleared");
            }

            // Process preferred provider changes
            if (!string.IsNullOrWhiteSpace(modal.PreferredProvider))
            {
                var provider = modal.PreferredProvider.Trim();
                var validProviders = new[] { "OpenAI", "Anthropic", "Grok", "Gemini" };
                var matchedProvider = validProviders.FirstOrDefault(p => 
                    string.Equals(p, provider, StringComparison.OrdinalIgnoreCase));

                if (matchedProvider != null)
                {
                    if (serverMeta.PreferredProvider != matchedProvider)
                    {
                        serverMeta.PreferredProvider = matchedProvider;
                        changes.Add($"✅ **Preferred Provider** set to {matchedProvider}");
                    }
                }
                else
                {
                    changes.Add($"❌ **Preferred Provider** - Invalid provider: {provider}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(serverMeta.PreferredProvider))
            {
                serverMeta.PreferredProvider = null;
                changes.Add("🔄 **Preferred Provider** cleared (using default)");
            }

            // Save changes if any were made
            if (changes.Any(c => c.StartsWith("✅") || c.StartsWith("🔄")))
            {
                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
            }

            // Create response embed
            var embedBuilder = new EmbedBuilder()
                .WithTitle("⚙️ Configuration Updated")
                .WithColor(changes.Any(c => c.StartsWith("❌")) ? Color.Orange : Color.Green)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithCurrentTimestamp();

            if (changes.Any())
            {
                embedBuilder.WithDescription(string.Join("\n", changes));
            }
            else
            {
                embedBuilder.WithDescription("ℹ️ No changes were made to the configuration.");
            }

            embedBuilder.AddField("💡 Tip", "Use `/admin config view` to see your complete configuration", inline: false);

            await RespondAsync(embed: embedBuilder.Build());

            _logger.LogInformation("Admin {UserId} completed interactive setup for server {ServerId} with {ChangeCount} changes",
                Context.User.Id, Context.Guild.Id, changes.Count(c => c.StartsWith("✅") || c.StartsWith("🔄")));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling config modal for server {ServerId}", Context.Guild?.Id);
            await RespondAsync("❌ An error occurred while processing your configuration.", ephemeral: true);
        }
    }
}

/// <summary>
/// Modal class for interactive configuration setup
/// </summary>
public class InteractiveConfigModal : IModal
{
    public string Title => "Server Configuration";

    [InputLabel("Server Persona")]
    [ModalTextInput("persona_input", TextInputStyle.Paragraph, "Enter a custom persona for the AI assistant...", maxLength: 4000)]
    public string Persona { get; set; } = string.Empty;

    [InputLabel("Primary Channel ID")]
    [ModalTextInput("primary_channel_input", TextInputStyle.Short, "Enter the channel ID for primary bot activity", maxLength: 20)]
    public string PrimaryChannelId { get; set; } = string.Empty;

    [InputLabel("Preferred AI Provider")]
    [ModalTextInput("preferred_provider_input", TextInputStyle.Short, "OpenAI, Anthropic, Grok, Gemini, or leave empty", maxLength: 50)]
    public string PreferredProvider { get; set; } = string.Empty;
}