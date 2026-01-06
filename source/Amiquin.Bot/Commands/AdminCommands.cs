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

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay("# ✅ Persona Updated");
                container.WithTextDisplay("Server persona has been updated successfully.");
                container.WithTextDisplay($"**New Persona:**\n{(persona.Length > 500 ? $"{persona[..500]}..." : persona)}");
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
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

        var componentsBuilder = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}");
                container.WithTextDisplay(message);

                if (!string.IsNullOrWhiteSpace(thumbnail))
                {
                    container.WithTextDisplay($"**Image:** [View]({thumbnail})");
                }

                if (withAuthor)
                {
                    container.WithTextDisplay($"**Author:** {Context.User.Username}");
                }
            });

        var components = componentsBuilder.Build();

        await Context.Channel.SendMessageAsync(components: components, flags: MessageFlags.ComponentsV2);
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

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay("# ⚙️ Toggle Updated");
                container.WithTextDisplay("Feature toggle has been updated successfully.");
                container.WithTextDisplay($"**Feature:** {toggleName}\n**Status:** {(isEnabled ? "✅ Enabled" : "❌ Disabled")}");
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
        _logger.LogInformation("Admin {UserId} toggled {Toggle} to {State} for guild {GuildId}",
            Context.User.Id, toggleName, isEnabled, Context.Guild.Id);
    }

    [SlashCommand("nuke", "Delete multiple messages from the channel")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    [Ephemeral]
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
                // Delete messages first
                await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messagesToDelete);

                // Update the ephemeral response (which won't be deleted)
                var components = new ComponentBuilderV2()
                    .WithContainer(container =>
                    {
                        container.WithTextDisplay("# 🗑️ Messages Deleted");
                        container.WithTextDisplay($"Successfully deleted **{messagesToDelete.Count}** messages.");
                        container.WithTextDisplay($"*Requested by {Context.User.Username}*");
                    })
                    .Build();

                try
                {
                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                }
                catch (Discord.Net.HttpException httpEx) when (httpEx.DiscordCode == Discord.DiscordErrorCode.UnknownMessage)
                {
                    _logger.LogWarning("Original response was deleted during nuke operation");
                    // Response was deleted, that's okay for this command
                }
            }
            else
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ No messages found that can be deleted (messages must be less than 14 days old).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nuke command for guild {GuildId}", Context.Guild.Id);
            try
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while deleting messages.");
            }
            catch
            {
                // If we can't even modify the response, log and move on
                _logger.LogWarning("Could not update response after nuke error");
            }
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

            var avatarUrl = Context.Client.CurrentUser.GetDisplayAvatarUrl(ImageFormat.Auto, 1024);

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay("# 😌 Calming Down...");
                    container.WithTextDisplay($"**{Context.Client.CurrentUser.Username}** - Taking a break");
                    container.WithTextDisplay("I'll take it easy for a bit. My engagement has been reset to baseline.");
                    container.WithTextDisplay($"**Engagement Level:** Reset to {currentLevel:F1}x (baseline)\n**Context:** Cleared all conversation context\n**Status:** 🌙 Relaxed mode activated");
                    container.WithTextDisplay("*Use mentions to re-engage me if needed*");

                    // Add avatar to media gallery
                    container.WithMediaGallery([avatarUrl]);
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

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

            var avatarUrl = Context.Client.CurrentUser.GetDisplayAvatarUrl(ImageFormat.Auto, 1024);

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# {(success ? "📦 History Compacted" : "⚠️ Compaction Issue")}");

                    // Add avatar as a section header  
                    container.AddComponent(new SectionBuilder()
                        .AddComponent(new TextDisplayBuilder()
                            .WithContent($"**{Context.Client.CurrentUser.Username}** - History Optimization")));

                    container.WithTextDisplay(message);

                    if (success)
                    {
                        container.WithTextDisplay("**💾 Benefits:**\n• Reduced memory usage\n• Faster response times\n• Lower token costs");
                        container.WithTextDisplay("**🔄 What happened:**\n• Older messages summarized\n• Recent messages preserved\n• Context maintained");
                        container.WithTextDisplay("*History optimization helps maintain performance*");
                    }
                    else
                    {
                        container.WithTextDisplay("*Try again later or check if there are enough messages to compact*");
                    }

                    // Add avatar to media gallery
                    container.WithMediaGallery([avatarUrl]);
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

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
        [Summary("model", "The AI model to use")][Autocomplete(typeof(ModelAutoCompleteHandler))] string model)
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

            // Update server meta with both provider and model
            var serverMeta = _botContextAccessor.ServerMeta;
            if (serverMeta != null)
            {
                serverMeta.PreferredProvider = provider;
                serverMeta.PreferredModel = model;
                await _serverMetaService.UpdateServerMetaAsync(serverMeta);
            }

            // Update active sessions
            var updatedCount = await _chatSessionService.UpdateServerSessionModelAsync(serverId, model, provider);

            var components = new ComponentBuilderV2()
                .WithTextDisplay("# ✅ AI Model Updated")
                .WithTextDisplay(updatedCount > 0
                    ? $"Updated **{updatedCount}** active session(s) to use **{model}** from **{provider}**"
                    : $"Server will now use **{model}** from **{provider}** for new chat sessions")
                .WithTextDisplay($"**Model:** {model}")
                .WithTextDisplay($"**Provider:** {provider}")
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

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
    [Ephemeral]
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

                    var alreadySleepingComponents = new ComponentBuilderV2()
                        .WithTextDisplay("# 😴 Already Sleeping")
                        .WithTextDisplay($"I'm already sleeping! I'll wake up in about **{remainingMinutes} minutes**.")
                        .WithTextDisplay("*Use `/admin wake-up` to wake me up early.*")
                        .Build();

                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = alreadySleepingComponents;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                    return;
                }
            }

            // Put bot to sleep
            var wakeUpTime = await _sleepService.PutToSleepAsync(Context.Guild.Id, minutes);

            var components = new ComponentBuilderV2()
                .WithTextDisplay("# 😴 Going to Sleep (Admin)")
                .WithTextDisplay($"I'm going to sleep for **{minutes} minutes** as requested by an admin.")
                .WithTextDisplay($"**💤 Sleep Duration:** {minutes} minutes ({TimeSpan.FromMinutes(minutes):h\\:mm})")
                .WithTextDisplay($"**⏰ Wake Up Time:** <t:{((DateTimeOffset)wakeUpTime).ToUnixTimeSeconds()}:F>")
                .WithTextDisplay($"**👮 Requested By:** {Context.User.Mention}")
                .WithTextDisplay("*Use /admin wake-up to end sleep early*")
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });

            _logger.LogInformation("Admin {UserId} put bot to sleep for {Minutes} minutes on server {ServerId}",
                Context.User.Id, minutes, Context.Guild.Id);
        }
        catch (ArgumentException ex)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error putting bot to sleep on server {ServerId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while putting the bot to sleep.");
        }
    }

    [SlashCommand("wake-up", "Wake up Amiquin if it's sleeping")]
    [Ephemeral]
    public async Task WakeUpAsync()
    {
        try
        {
            var wasAwake = await _sleepService.WakeUpAsync(Context.Guild.Id);

            if (wasAwake)
            {
                var components = new ComponentBuilderV2()
                    .WithTextDisplay("# ☀️ Good Morning!")
                    .WithTextDisplay("I'm awake now! Thanks for waking me up early.")
                    .WithTextDisplay($"**👮 Woken By:** {Context.User.Mention}")
                    .WithTextDisplay("*Ready to assist!*")
                    .Build();

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = components;
                    msg.Flags = MessageFlags.ComponentsV2;
                    msg.Embed = null;
                    msg.Content = null;
                });

                _logger.LogInformation("Admin {UserId} woke up bot on server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            else
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "☀️ I'm already awake! No need to wake me up.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waking up bot on server {ServerId}", Context.Guild.Id);
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while waking up the bot.");
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

        [SlashCommand("server-manager", "Open interactive server configuration interface with detailed view")]
        public async Task ServerManagerAsync()
        {
            try
            {
                var components = await _configurationService.CreateConfigurationInterfaceAsync(
                    Context.Guild.Id,
                    Context.Guild);

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = components;
                    msg.Flags = MessageFlags.ComponentsV2;
                    // Clear embed and content since ComponentsV2 uses display components
                    msg.Embed = null;
                    msg.Content = null;
                });

                _logger.LogInformation("Admin {UserId} opened server manager interface for server {ServerId}",
                    Context.User.Id, Context.Guild.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening server manager interface for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while loading the server manager interface.");
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

                ComponentBuilderV2? componentsBuilder = null;

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

                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# ✅ Persona Updated");
                                container.WithTextDisplay("Server persona has been updated successfully.");
                                container.WithTextDisplay($"**New Persona:**\n{(value.Length > 500 ? $"{value[..500]}..." : value)}");
                            });
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

                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# ✅ Primary Channel Updated");
                                container.WithTextDisplay($"Primary channel has been set to {channel.Mention}");
                                container.WithTextDisplay($"**Channel:** {channel.Mention}\n**Channel ID:** {channelId}");
                            });
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
                        // Clear the model when provider changes to avoid mismatched provider/model
                        serverMeta.PreferredModel = null;
                        await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# ✅ Provider Updated");
                                container.WithTextDisplay($"AI provider has been set to **{matchedProvider}**");
                                container.WithTextDisplay($"**Provider:** {matchedProvider}");
                                container.WithTextDisplay("*Note: Model has been reset to provider default. Use `/admin set-model` to select a specific model.*");
                            });
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

                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# ✅ NSFW Channel Updated");
                                container.WithTextDisplay($"NSFW channel has been set to {nsfwChannel.Mention}");
                                container.WithTextDisplay($"**Channel:** {nsfwChannel.Mention}\n**Channel ID:** {nsfwChannelId}");
                            });
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**");
                        return;
                }

                if (componentsBuilder != null)
                {
                    var components = componentsBuilder.Build();
                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                }

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

                ComponentBuilderV2? componentsBuilder = null;
                string settingDisplayName = setting.ToLowerInvariant();

                switch (setting.ToLowerInvariant())
                {
                    case "persona":
                        serverMeta.Persona = string.Empty;
                        settingDisplayName = "Server Persona";
                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# 🔄 Configuration Reset");
                                container.WithTextDisplay($"**{settingDisplayName}** has been reset to default.");
                                container.WithTextDisplay("The server persona is now empty and will use the global default.");
                            });
                        break;

                    case "primary-channel":
                    case "main-channel":
                    case "channel":
                        serverMeta.PrimaryChannelId = null;
                        settingDisplayName = "Primary Channel";
                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# 🔄 Configuration Reset");
                                container.WithTextDisplay($"**{settingDisplayName}** has been reset.");
                                container.WithTextDisplay("The bot will now respond in any channel where it's mentioned.");
                            });
                        break;

                    case "provider":
                        serverMeta.PreferredProvider = null;
                        serverMeta.PreferredModel = null; // Also reset model when resetting provider
                        settingDisplayName = "AI Provider";
                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# 🔄 Configuration Reset");
                                container.WithTextDisplay($"**{settingDisplayName}** has been reset to default.");
                                container.WithTextDisplay("The server will now use the global default AI provider and model.");
                            });
                        break;

                    case "nsfw-channel":
                    case "nsfw_channel":
                        serverMeta.NsfwChannelId = null;
                        settingDisplayName = "NSFW Channel";
                        componentsBuilder = new ComponentBuilderV2()
                            .WithContainer(container =>
                            {
                                container.WithTextDisplay("# 🔄 Configuration Reset");
                                container.WithTextDisplay($"**{settingDisplayName}** has been reset.");
                                container.WithTextDisplay("NSFW features are now disabled for this server.");
                            });
                        break;

                    default:
                        await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Unknown setting: **{setting}**");
                        return;
                }

                await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                if (componentsBuilder != null)
                {
                    var components = componentsBuilder.Build();
                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                }

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
                export.AppendLine($"- Preferred Model: {serverMeta?.PreferredModel ?? "Provider default"}");
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

        [SlashCommand("import", "Import server configuration from JSON")]
        [Ephemeral]
        public async Task ImportConfigAsync()
        {
            try
            {
                var modal = new ModalBuilder()
                    .WithTitle("Import Server Configuration")
                    .WithCustomId("config_import_modal")
                    .AddTextInput("Configuration JSON", "config_json", TextInputStyle.Paragraph,
                        "Paste exported configuration JSON here...",
                        required: true,
                        minLength: 10,
                        maxLength: 4000)
                    .Build();

                await Context.Interaction.RespondWithModalAsync(modal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing import modal for server {ServerId}", Context.Guild.Id);
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while preparing the import.");
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