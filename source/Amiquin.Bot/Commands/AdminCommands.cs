using Amiquin.Bot.Commands.AutoComplete;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.Configuration;
using Amiquin.Core.Services.Meta;
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

    public AdminCommands(
        ILogger<AdminCommands> logger, 
        IServerMetaService serverMetaService, 
        IToggleService toggleService, 
        IChatSessionService chatSessionService, 
        BotContextAccessor botContextAccessor,
        IConfigurationInteractionService configurationService)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _chatSessionService = chatSessionService;
        _botContextAccessor = botContextAccessor;
        _configurationService = configurationService;
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

    [SlashCommand("set-model", "Set the AI model for the server")]
    public async Task SetModelAsync(
        [Summary("model", "The AI model to use")] string model,
        [Summary("provider", "The AI provider")] [Choice("OpenAI", "OpenAI")] [Choice("Anthropic", "Anthropic")] [Choice("Gemini", "Gemini")] [Choice("Grok", "Grok")] string provider = "OpenAI")
    {
        try
        {
            var serverId = Context.Guild.Id;
            
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
            _logger.LogError(ex, "Error setting model {Model} from {Provider} for server {ServerId}", 
                model, provider, Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while updating the AI model.");
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
                var serverMeta = _botContextAccessor.ServerMeta;
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

                // List enabled features
                var enabledFeatures = toggles.Where(t => t.IsEnabled).Select(t => t.Name).ToList();
                if (enabledFeatures.Any())
                {
                    embed.AddField("✅ Enabled Features", 
                        string.Join(", ", enabledFeatures.Take(10)), 
                        false);
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
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found. Please try again later.");
                    return;
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
                var serverMeta = _botContextAccessor.ServerMeta;
                if (serverMeta == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Server metadata not found. Please try again later.");
                    return;
                }

                var embed = new EmbedBuilder()
                    .WithTitle("🔄 Configuration Reset")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp();

                switch (setting.ToLowerInvariant())
                {
                    case "persona":
                        serverMeta.Persona = null;
                        embed.WithDescription("Server persona has been reset to default.");
                        break;

                    case "primary-channel":
                    case "channel":
                        serverMeta.PrimaryChannelId = null;
                        embed.WithDescription("Primary channel has been reset.");
                        break;

                    case "provider":
                        serverMeta.PreferredProvider = null;
                        embed.WithDescription("AI provider has been reset to default.");
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
    }
}