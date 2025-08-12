using Amiquin.Core.Models;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Modal;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Amiquin.Core.Services.Configuration;

/// <summary>
/// Implementation of configuration interaction service using Discord Components.
/// </summary>
public class ConfigurationInteractionService : IConfigurationInteractionService
{
    private readonly ILogger<ConfigurationInteractionService> _logger;
    private readonly IComponentHandlerService _componentHandler;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleService _toggleService;
    private readonly IPaginationService _paginationService;
    private readonly IModalService _modalService;

    // Component prefixes
    private const string ConfigMenuPrefix = "config_menu";
    private const string ConfigActionPrefix = "config_action";
    private const string QuickSetupPrefix = "config_quick";
    private const string TogglePrefix = "config_toggle";
    private const string ModalPrefix = "config_modal";
    private const string NavigationPrefix = "config_nav";

    public ConfigurationInteractionService(
        ILogger<ConfigurationInteractionService> logger,
        IComponentHandlerService componentHandler,
        IServerMetaService serverMetaService,
        IToggleService toggleService,
        IPaginationService paginationService,
        IModalService modalService)
    {
        _logger = logger;
        _componentHandler = componentHandler;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _paginationService = paginationService;
        _modalService = modalService;
    }

    public void Initialize()
    {
        _logger.LogInformation("Initializing configuration interaction handlers");

        // Register main menu handler
        _componentHandler.RegisterHandler(ConfigMenuPrefix, HandleConfigMenuAsync);

        // Register action handlers
        _componentHandler.RegisterHandler(ConfigActionPrefix, HandleConfigActionAsync);

        // Register quick setup handlers
        _componentHandler.RegisterHandler(QuickSetupPrefix, HandleQuickSetupAsync);

        // Register toggle handlers
        _componentHandler.RegisterHandler(TogglePrefix, HandleToggleAsync);

        // Register navigation handlers
        _componentHandler.RegisterHandler(NavigationPrefix, HandleNavigationAsync);

        // Register modal handlers
        _modalService.RegisterHandler(ModalPrefix, HandleModalSubmissionAsync);

        // Register modal triggers (specific interactions that will respond with modals)
        _componentHandler.RegisterModalTrigger($"{QuickSetupPrefix}:persona"); // Quick setup persona
        _componentHandler.RegisterModalTrigger($"{ConfigActionPrefix}:persona"); // Config action persona (if needed)

        _logger.LogInformation("Configuration interaction handlers registered successfully");
    }

    public async Task<MessageComponent> CreateConfigurationInterfaceAsync(ulong guildId, SocketGuild guild)
    {
        // Ensure server metadata exists first
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta == null)
        {
            // Create server metadata if it doesn't exist
            serverMeta = await _serverMetaService.CreateServerMetaAsync(guildId, guild.Name);
            _logger.LogInformation("Created server metadata for new server {ServerName} ({ServerId})", guild.Name, guildId);
        }

        // Now ensure all toggles are created for this server
        await _toggleService.CreateServerTogglesIfNotExistsAsync(guildId);

        // Build the complete configuration using ComponentsV2 display components
        return await BuildCompleteConfigurationComponentsV2Async(serverMeta, guild);
    }

    private async Task<Embed> BuildConfigurationEmbedAsync(Models.ServerMeta serverMeta, SocketGuild guild)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle($"⚙️ Server Configuration")
            .WithDescription($"Configure settings for **{guild.Name}**")
            .WithColor(new Color(88, 101, 242)) // Discord blurple
            .WithThumbnailUrl(guild.IconUrl)
            .WithCurrentTimestamp()
            .WithFooter("Use the components below to configure your server");

        // Build status summary
        var statusBuilder = new StringBuilder();

        // Persona status
        var personaIcon = !string.IsNullOrWhiteSpace(serverMeta.Persona) ? "✅" : "⚠️";
        var personaText = !string.IsNullOrWhiteSpace(serverMeta.Persona)
            ? TruncateText(serverMeta.Persona, 50)
            : "Not configured";
        statusBuilder.AppendLine($"{personaIcon} **Persona:** {personaText}");

        // Primary channel status
        var channelIcon = serverMeta.PrimaryChannelId.HasValue ? "✅" : "⚠️";
        var channelText = "Not configured";
        if (serverMeta.PrimaryChannelId.HasValue)
        {
            var channel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            channelText = channel != null ? channel.Mention : "Channel not found";
        }
        statusBuilder.AppendLine($"{channelIcon} **Primary Channel:** {channelText}");

        // AI Provider status
        var providerIcon = !string.IsNullOrWhiteSpace(serverMeta.PreferredProvider) ? "✅" : "ℹ️";
        var providerText = !string.IsNullOrWhiteSpace(serverMeta.PreferredProvider)
            ? serverMeta.PreferredProvider
            : "Using default";
        statusBuilder.AppendLine($"{providerIcon} **AI Provider:** {providerText}");

        // NSFW Channel status
        var nsfwChannelIcon = serverMeta.NsfwChannelId.HasValue ? "✅" : "⚠️";
        var nsfwChannelText = "Not configured";
        if (serverMeta.NsfwChannelId.HasValue)
        {
            var nsfwChannel = guild.GetTextChannel(serverMeta.NsfwChannelId.Value);
            nsfwChannelText = nsfwChannel != null ? nsfwChannel.Mention : "Channel not found";
        }
        statusBuilder.AppendLine($"{nsfwChannelIcon} **NSFW Channel:** {nsfwChannelText}");

        // Get toggle count
        var toggles = await _toggleService.GetTogglesByServerId(guild.Id);
        var enabledCount = toggles.Count(t => t.IsEnabled);
        statusBuilder.AppendLine($"🎛️ **Features:** {enabledCount}/{toggles.Count} enabled");

        embedBuilder.AddField("📊 Current Configuration", statusBuilder.ToString(), inline: false);

        // Add helpful tips
        var tipsBuilder = new StringBuilder();
        tipsBuilder.AppendLine("• Use the **dropdown menu** to navigate sections");
        tipsBuilder.AppendLine("• Click **Quick Setup** buttons for common tasks");
        tipsBuilder.AppendLine("• Toggle features on/off with the buttons");

        embedBuilder.AddField("💡 Tips", tipsBuilder.ToString(), inline: false);

        return embedBuilder.Build();
    }

    private async Task<MessageComponent> BuildConfigurationComponentsAsync(Models.ServerMeta serverMeta, SocketGuild guild)
    {
        var builder = new ComponentBuilderV2();

        // Main Navigation Menu
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix, guild.Id.ToString()))
            .WithPlaceholder("Select a configuration section...")
            .AddOption("Server Persona", "persona", "Configure AI assistant behavior", new Emoji("🎭"))
            .AddOption("Primary Channel", "channel", "Set main bot channel", new Emoji("💬"))
            .AddOption("NSFW Channel", "nsfw_channel", "Set NSFW content channel", new Emoji("🔞"))
            .AddOption("AI Provider", "provider", "Choose AI model provider", new Emoji("🤖"))
            .AddOption("Feature Toggles", "toggles", "Enable/disable features", new Emoji("🎛️"))
            .AddOption("View All Settings", "view_all", "Show complete configuration", new Emoji("📋"))
            .AddOption("Discord Server Info", "server_info", "Discord server details and statistics", new Emoji("ℹ️"))
            .AddOption("Amiquin Metadata", "amiquin_metadata", "Bot configuration and AI settings", new Emoji("⚙️"))
            .AddOption("Session Context", "session_context", "Current conversation context and stats", new Emoji("💭"))
            .AddOption("Server Persona Details", "persona_details", "Full server persona configuration", new Emoji("📖"));

        builder.WithActionRow([selectMenu]);

        // Quick Actions Row
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guild.Id.ToString()))
                .WithLabel("Set Persona")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🎭")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "channel", guild.Id.ToString()))
                .WithLabel("Set Channel")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("💬")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "provider", guild.Id.ToString()))
                .WithLabel("Set Provider")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🤖")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "nsfw_channel", guild.Id.ToString()))
                .WithLabel("Set NSFW Channel")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("🔞"))
        ]);

        // Most Important Toggles - Each in their own row for prominence
        var toggles = await _toggleService.GetTogglesByServerId(guild.Id);
        var criticalToggles = toggles
            .Where(t => t.Name == Constants.ToggleNames.EnableChat ||
                       t.Name == Constants.ToggleNames.EnableDailyNSFW)
            .ToList();

        var importantToggles = toggles
            .Where(t => t.Name == Constants.ToggleNames.EnableTTS ||
                       t.Name == Constants.ToggleNames.EnableAIWelcome)
            .ToList();

        int toggleRow = 2;

        // Critical toggles get their own rows (most important)
        foreach (var toggle in criticalToggles)
        {
            var emoji = toggle.IsEnabled ? "✅" : "❌";
            var style = toggle.IsEnabled ? ButtonStyle.Success : ButtonStyle.Secondary;
            var label = $"{emoji} {FormatToggleName(toggle.Name)}";

            builder.WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name, guild.Id.ToString()))
                    .WithLabel(label)
                    .WithStyle(style)
            ]);
            toggleRow++;
        }

        // Important toggles can share a row (secondary importance)
        if (importantToggles.Any())
        {
            var importantButtons = new List<ButtonBuilder>();
            for (int i = 0; i < importantToggles.Count && i < 2; i++)
            {
                var toggle = importantToggles[i];
                var emoji = toggle.IsEnabled ? "✅" : "❌";
                var style = toggle.IsEnabled ? ButtonStyle.Success : ButtonStyle.Secondary;
                var label = $"{emoji} {FormatToggleName(toggle.Name)}";

                importantButtons.Add(
                    new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name, guild.Id.ToString()))
                        .WithLabel(label)
                        .WithStyle(style));
            }
            builder.WithActionRow([.. importantButtons]);
        }

        // Footer Actions - Use the next available row (max 4, 0-indexed)
        var footerRow = Math.Min(toggleRow + 1, 4);
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "refresh", guild.Id.ToString()))
                .WithLabel("Refresh")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("🔄")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "export", guild.Id.ToString()))
                .WithLabel("Export")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("📤")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "help", guild.Id.ToString()))
                .WithLabel("Help")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("❓"))
        ]);

        return builder.Build();
    }

    private async Task<MessageComponent> BuildCompleteConfigurationComponentsV2Async(Models.ServerMeta serverMeta, SocketGuild guild)
    {
        var toggles = await _toggleService.GetTogglesByServerId(guild.Id);
        var builder = new ComponentBuilderV2();

        // Header with server name
        builder.WithTextDisplay($"# 📋 Server Configuration\n## {guild.Name}");

        // Server Persona Section
        var personaContent = !string.IsNullOrWhiteSpace(serverMeta?.Persona)
            ? $"**🎭 Persona**\n```\n{TruncateText(serverMeta.Persona, 300)}\n```"
            : "**🎭 Persona**\n*Not configured*";

        builder.WithTextDisplay(personaContent);

        // Channel and Provider Information
        var channelText = "*Not configured*";
        if (serverMeta?.PrimaryChannelId.HasValue == true)
        {
            var channel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            channelText = channel != null ? channel.Mention : "*Channel not found*";
        }

        var nsfwChannelText = "*Not configured*";
        if (serverMeta?.NsfwChannelId.HasValue == true)
        {
            var nsfwChannel = guild.GetTextChannel(serverMeta.NsfwChannelId.Value);
            nsfwChannelText = nsfwChannel != null ? nsfwChannel.Mention : "*Channel not found*";
        }

        var providerText = !string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider)
            ? serverMeta.PreferredProvider
            : "*Using default*";

        builder.WithTextDisplay($"**💬 Primary Channel:** {channelText}\n**🔞 NSFW Channel:** {nsfwChannelText}\n**🤖 AI Provider:** {providerText}");

        // Feature Toggles
        var enabledToggles = toggles.Where(t => t.IsEnabled).Select(t => FormatToggleName(t.Name)).ToList();
        var disabledToggles = toggles.Where(t => !t.IsEnabled).Select(t => FormatToggleName(t.Name)).ToList();

        if (enabledToggles.Any())
        {
            var enabledText = $"**✅ Enabled Features**\n{string.Join("\n", enabledToggles.Take(10))}";
            builder.WithTextDisplay(enabledText);
        }

        if (disabledToggles.Any())
        {
            var disabledText = $"**❌ Disabled Features**\n{string.Join("\n", disabledToggles.Take(10))}";
            builder.WithTextDisplay(disabledText);
        }

        // Interactive Components - Navigation Menu
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix, guild.Id.ToString()))
            .WithPlaceholder("Configure specific settings...")
            .AddOption("Server Persona", "persona", "Configure AI assistant behavior", new Emoji("🎭"))
            .AddOption("Primary Channel", "channel", "Set main bot channel", new Emoji("💬"))
            .AddOption("NSFW Channel", "nsfw_channel", "Set NSFW content channel", new Emoji("🔞"))
            .AddOption("AI Provider", "provider", "Choose AI model provider", new Emoji("🤖"))
            .AddOption("Feature Toggles", "toggles", "Enable/disable features", new Emoji("🎛️"))
            .AddOption("Discord Server Info", "server_info", "Discord server details and statistics", new Emoji("ℹ️"))
            .AddOption("Amiquin Metadata", "amiquin_metadata", "Bot configuration and AI settings", new Emoji("⚙️"))
            .AddOption("Session Context", "session_context", "Current conversation context and stats", new Emoji("💭"))
            .AddOption("Server Persona Details", "persona_details", "Full server persona configuration", new Emoji("📖"));

        builder.WithActionRow([selectMenu]);

        // Quick Actions Row
        var quickActionButtons = new List<ButtonBuilder>
        {
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guild.Id.ToString()))
                .WithLabel("Set Persona")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🎭")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "channel", guild.Id.ToString()))
                .WithLabel("Set Channel")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("💬")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "provider", guild.Id.ToString()))
                .WithLabel("Set Provider")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🤖")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "nsfw_channel", guild.Id.ToString()))
                .WithLabel("Set NSFW Channel")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("🔞"))
        };

        builder.WithActionRow(quickActionButtons);

        // Management Actions Row
        var managementButtons = new List<ButtonBuilder>
        {
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "export", guild.Id.ToString()))
                .WithLabel("📤 Export")
                .WithStyle(ButtonStyle.Primary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "refresh", guild.Id.ToString()))
                .WithLabel("🔄 Refresh")
                .WithStyle(ButtonStyle.Secondary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "help", guild.Id.ToString()))
                .WithLabel("❓ Help")
                .WithStyle(ButtonStyle.Secondary)
        };

        builder.WithActionRow(managementButtons);

        return builder.Build();
    }

    private async Task<ActionRowBuilder[]> BuildCompleteConfigurationComponentsAsync(Models.ServerMeta serverMeta, SocketGuild guild)
    {
        var components = new List<ActionRowBuilder>();

        // Main Navigation Menu
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix, guild.Id.ToString()))
            .WithPlaceholder("Configure specific settings...")
            .AddOption("Server Persona", "persona", "Configure AI assistant behavior", new Emoji("🎭"))
            .AddOption("Primary Channel", "channel", "Set main bot channel", new Emoji("💬"))
            .AddOption("NSFW Channel", "nsfw_channel", "Set NSFW content channel", new Emoji("🔞"))
            .AddOption("AI Provider", "provider", "Choose AI model provider", new Emoji("🤖"))
            .AddOption("Feature Toggles", "toggles", "Enable/disable features", new Emoji("🎛️"))
            .AddOption("Discord Server Info", "server_info", "Discord server details and statistics", new Emoji("ℹ️"))
            .AddOption("Amiquin Metadata", "amiquin_metadata", "Bot configuration and AI settings", new Emoji("⚙️"))
            .AddOption("Session Context", "session_context", "Current conversation context and stats", new Emoji("💭"))
            .AddOption("Server Persona Details", "persona_details", "Full server persona configuration", new Emoji("📖"));

        components.Add(new ActionRowBuilder().WithSelectMenu(selectMenu));

        // Quick Actions Row
        components.Add(new ActionRowBuilder().WithComponents([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guild.Id.ToString()))
                .WithLabel("Set Persona")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🎭")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "channel", guild.Id.ToString()))
                .WithLabel("Set Channel")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("💬")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "provider", guild.Id.ToString()))
                .WithLabel("Set Provider")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🤖")),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "nsfw_channel", guild.Id.ToString()))
                .WithLabel("Set NSFW Channel")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("🔞"))
        ]));

        // Export and Refresh Actions
        components.Add(new ActionRowBuilder().WithComponents([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "export", guild.Id.ToString()))
                .WithLabel("📤 Export")
                .WithStyle(ButtonStyle.Primary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "refresh", guild.Id.ToString()))
                .WithLabel("🔄 Refresh")
                .WithStyle(ButtonStyle.Secondary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "help", guild.Id.ToString()))
                .WithLabel("❓ Help")
                .WithStyle(ButtonStyle.Secondary)
        ]));

        return components.ToArray();
    }

    private async Task<bool> HandleConfigMenuAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 1 || !ulong.TryParse(context.Parameters[0], out var guildId))
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid configuration data.");
                return true;
            }

            // Verify user has permission
            var guild = component.User as SocketGuildUser;
            if (guild == null || !guild.GuildPermissions.ModerateMembers)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ You need Moderate Members permission to configure the server.");
                return true;
            }

            var selectedValue = component.Data.Values.FirstOrDefault();

            switch (selectedValue)
            {
                case "persona":
                    await ShowPersonaConfigurationAsync(component, guildId);
                    break;
                case "channel":
                    await ShowChannelConfigurationAsync(component, guildId);
                    break;
                case "nsfw_channel":
                    await ShowNsfwChannelConfigurationAsync(component, guildId);
                    break;
                case "provider":
                    await ShowProviderConfigurationAsync(component, guildId);
                    break;
                case "toggles":
                    await ShowToggleConfigurationAsync(component, guildId);
                    break;
                case "view_all":
                    await ShowCompleteConfigurationAsync(component, guildId);
                    break;
                case "server_info":
                    await ShowDiscordServerInfoAsync(component, guildId);
                    break;
                case "amiquin_metadata":
                    await ShowAmiquinMetadataAsync(component, guildId);
                    break;
                case "session_context":
                    await ShowSessionContextAsync(component, guildId);
                    break;
                case "persona_details":
                    await ShowPersonaDetailsAsync(component, guildId);
                    break;
                default:
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unknown configuration option.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling config menu interaction");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while processing your selection.");
            return true;
        }
    }

    private async Task<bool> HandleConfigActionAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 2)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid action data.");
                return true;
            }

            var action = context.Parameters[0];
            var guildId = ulong.Parse(context.Parameters[1]);

            // Handle specific configuration actions
            switch (action)
            {
                case "set_channel":
                    if (component.Data.Values.Any())
                    {
                        var channelId = ulong.Parse(component.Data.Values.First());
                        await SetPrimaryChannelAsync(component, guildId, channelId);
                    }
                    break;

                case "set_nsfw_channel":
                    if (component.Data.Values.Any())
                    {
                        var channelId = ulong.Parse(component.Data.Values.First());
                        await SetNsfwChannelAsync(component, guildId, channelId);
                    }
                    break;
                case "set_provider":
                    if (component.Data.Values.Any())
                    {
                        var provider = component.Data.Values.First();
                        await SetProviderAsync(component, guildId, provider);
                    }
                    break;
                case "clear_persona":
                case "clear_channel":
                case "clear_nsfw_channel":
                case "clear_provider":
                    await ClearSettingAsync(component, guildId, action.Replace("clear_", ""));
                    break;
                default:
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unknown action.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling config action");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while processing your action.");
            return true;
        }
    }

    private async Task<bool> HandleQuickSetupAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            if (context.Parameters.Length < 2)
            {
                if (component.HasResponded)
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid quick setup data.");
                else
                    await component.RespondAsync("❌ Invalid quick setup data.", ephemeral: true);
                return true;
            }

            var setupType = context.Parameters[0];
            var guildId = ulong.Parse(context.Parameters[1]);

            switch (setupType)
            {
                case "persona":
                    // Show modal for persona input (NOT deferred - handled specially in EventHandlerService)
                    var personaModal = new ModalBuilder()
                        .WithTitle("Set Server Persona")
                        .WithCustomId(_componentHandler.GenerateCustomId(ModalPrefix, "persona", guildId.ToString()))
                        .AddTextInput("Persona Description", "persona_input", TextInputStyle.Paragraph,
                            "Describe how the AI should behave in this server...",
                            required: true,
                            minLength: 10,
                            maxLength: 2000,
                            value: null)
                        .Build();

                    await component.RespondWithModalAsync(personaModal);
                    break;

                case "channel":
                    // Component is already deferred by EventHandlerService
                    await ShowChannelConfigurationAsync(component, guildId);
                    break;

                case "nsfw_channel":
                    // Component is already deferred by EventHandlerService
                    await ShowNsfwChannelConfigurationAsync(component, guildId);
                    break;

                case "provider":
                    // Component is already deferred by EventHandlerService
                    await ShowProviderConfigurationAsync(component, guildId);
                    break;

                default:
                    if (component.HasResponded)
                        await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unknown quick setup option.");
                    else
                        await component.RespondAsync("❌ Unknown quick setup option.", ephemeral: true);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling quick setup");

            try
            {
                if (component.HasResponded)
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred during quick setup.");
                else
                    await component.RespondAsync("❌ An error occurred during quick setup.", ephemeral: true);
            }
            catch { }

            return true;
        }
    }

    private async Task<bool> HandleToggleAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 2)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid toggle data.");
                return true;
            }

            var toggleName = context.Parameters[0];
            var guildId = ulong.Parse(context.Parameters[1]);

            // Get current toggle state
            var toggles = await _toggleService.GetTogglesByServerId(guildId);
            var toggle = toggles.FirstOrDefault(t => t.Name == toggleName);

            if (toggle != null)
            {
                // Toggle the state
                var newState = !toggle.IsEnabled;
                await _toggleService.SetServerToggleAsync(guildId, toggleName, newState);

                // Return to main configuration interface
                var guild = (component.Channel as SocketGuildChannel)?.Guild;
                if (guild != null)
                {
                    var components = await CreateConfigurationInterfaceAsync(guildId, guild);
                    await component.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                }
            }
            else
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Toggle not found.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling toggle");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while toggling the feature.");
            return true;
        }
    }

    private async Task<bool> HandleNavigationAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 2)
            {
                await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid navigation data.");
                return true;
            }

            var action = context.Parameters[0];
            var guildId = ulong.Parse(context.Parameters[1]);

            switch (action)
            {
                case "refresh":
                case "back":
                    var guild = (component.Channel as SocketGuildChannel)?.Guild;
                    if (guild != null)
                    {
                        var components = await CreateConfigurationInterfaceAsync(guildId, guild);
                        await component.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Components = components;
                            msg.Flags = MessageFlags.ComponentsV2;
                            msg.Embed = null;
                            msg.Content = null;
                        });
                    }
                    break;

                case "export":
                    await ExportConfigurationAsync(component, guildId);
                    break;

                case "help":
                    await ShowHelpAsync(component);
                    break;

                default:
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unknown navigation action.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling navigation");
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred during navigation.");
            return true;
        }
    }

    private async Task ShowPersonaConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);

        var embedBuilder = new EmbedBuilder()
            .WithTitle("🎭 Server Persona Configuration")
            .WithDescription("Configure how the AI assistant behaves in your server")
            .WithColor(new Color(155, 89, 182))
            .WithCurrentTimestamp();

        if (!string.IsNullOrWhiteSpace(serverMeta?.Persona))
        {
            embedBuilder.AddField("Current Persona", $"```{TruncateText(serverMeta.Persona, 1000)}```", false);
        }
        else
        {
            embedBuilder.AddField("Current Persona", "*Not configured*", false);
        }

        embedBuilder.AddField("💡 Tips for Writing a Good Persona",
            "• Be specific about the assistant's role and expertise\n" +
            "• Include desired communication style and tone\n" +
            "• Mention any specific knowledge areas or restrictions\n" +
            "• Keep it concise but comprehensive (under 2000 characters)",
            false);

        var builder = new ComponentBuilderV2();

        // Add action buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guildId.ToString()))
                .WithLabel("✏️ Edit Persona")
                .WithStyle(ButtonStyle.Primary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_persona", guildId.ToString()))
                .WithLabel("🗑️ Clear Persona")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(string.IsNullOrWhiteSpace(serverMeta?.Persona)),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowChannelConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var textChannels = guild.TextChannels
            .Where(c => guild.CurrentUser.GetPermissions(c).SendMessages)
            .OrderBy(c => c.Position)
            .Take(25)
            .ToList();

        var embedBuilder = new EmbedBuilder()
            .WithTitle("💬 Primary Channel Configuration")
            .WithDescription("Set the main channel where the bot will be most active")
            .WithColor(new Color(46, 204, 113))
            .WithCurrentTimestamp();

        if (serverMeta?.PrimaryChannelId.HasValue == true)
        {
            var currentChannel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            embedBuilder.AddField("Current Primary Channel",
                currentChannel != null ? currentChannel.Mention : "*Channel not found*",
                false);
        }
        else
        {
            embedBuilder.AddField("Current Primary Channel", "*Not configured*", false);
        }

        var builder = new ComponentBuilderV2();

        if (textChannels.Any())
        {
            // Create channel select menu
            var selectMenuOptions = new List<SelectMenuOptionBuilder>();
            foreach (var channel in textChannels)
            {
                var isSelected = serverMeta?.PrimaryChannelId == channel.Id;
                var topic = !string.IsNullOrWhiteSpace(channel.Topic)
                    ? (channel.Topic.Length > 50 ? channel.Topic[..50] + "..." : channel.Topic)
                    : "No topic set";
                selectMenuOptions.Add(new SelectMenuOptionBuilder(
                    $"#{channel.Name}",
                    channel.Id.ToString(),
                    topic,
                    isDefault: isSelected));
            }

            var selectMenu = new SelectMenuBuilder(
                _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_channel", guildId.ToString()),
                selectMenuOptions)
                .WithPlaceholder("Select a channel...")
                .WithMinValues(1)
                .WithMaxValues(1);

            builder.WithActionRow([selectMenu]);
        }

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_channel", guildId.ToString()))
                .WithLabel("🗑️ Clear Channel")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(serverMeta?.PrimaryChannelId == null),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowProviderConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var providers = new[] { "OpenAI", "Anthropic", "Gemini", "Grok" };

        var embedBuilder = new EmbedBuilder()
            .WithTitle("🤖 AI Provider Configuration")
            .WithDescription("Choose your preferred AI model provider")
            .WithColor(new Color(52, 152, 219))
            .WithCurrentTimestamp();

        embedBuilder.AddField("Current Provider",
            !string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider)
                ? $"**{serverMeta.PreferredProvider}**"
                : "*Using default*",
            false);

        embedBuilder.AddField("Available Providers",
            string.Join("\n", providers.Select(p => $"• **{p}**")),
            false);

        var builder = new ComponentBuilderV2();

        // Create provider select menu
        var selectMenuOptions = new List<SelectMenuOptionBuilder>();
        foreach (var provider in providers)
        {
            var isSelected = serverMeta?.PreferredProvider == provider;
            var description = provider switch
            {
                "OpenAI" => "GPT models",
                "Anthropic" => "Claude models",
                "Gemini" => "Google AI models",
                "Grok" => "xAI models",
                _ => ""
            };

            selectMenuOptions.Add(new SelectMenuOptionBuilder(
                provider,
                provider.ToLower(),
                description,
                isDefault: isSelected));
        }

        var selectMenu = new SelectMenuBuilder(
            _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_provider", guildId.ToString()),
            selectMenuOptions)
            .WithPlaceholder("Select a provider...")
            .WithMinValues(1)
            .WithMaxValues(1);

        builder.WithActionRow([selectMenu]);

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_provider", guildId.ToString()))
                .WithLabel("🔄 Use Default")
                .WithStyle(ButtonStyle.Secondary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowToggleConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        // Ensure all toggles are created for this server
        await _toggleService.CreateServerTogglesIfNotExistsAsync(guildId);

        var toggles = await _toggleService.GetTogglesByServerId(guildId);
        var guild = (component.Channel as SocketGuildChannel)?.Guild;

        if (guild == null)
        {
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unable to find guild information.");
            return;
        }

        // Generate all pages as embeds for pagination
        var embeds = GenerateToggleEmbeds(toggles.ToList(), guildId);

        if (embeds.Count == 1)
        {
            // Single page - show directly with buttons
            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embeds[0];
                msg.Components = GenerateToggleComponents(toggles.Take(8).ToList(), guildId, 0, 1);
            });
        }
        else
        {
            // Multiple pages - use pagination service
            var (embed, messageComponent) = await _paginationService.CreatePaginatedMessageAsync(embeds, component.User.Id);

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = messageComponent;
            });
        }
    }

    private List<Embed> GenerateToggleEmbeds(List<Models.Toggle> toggles, ulong guildId)
    {
        const int itemsPerPage = 8;
        var embeds = new List<Embed>();
        var totalPages = (int)Math.Ceiling((double)toggles.Count / itemsPerPage);
        var totalEnabled = toggles.Count(t => t.IsEnabled);

        for (int page = 0; page < totalPages; page++)
        {
            var startIndex = page * itemsPerPage;
            var pageToggles = toggles.Skip(startIndex).Take(itemsPerPage).ToList();
            var enabledCount = pageToggles.Count(t => t.IsEnabled);

            var embedBuilder = new EmbedBuilder()
                .WithTitle("🎛️ Feature Toggles")
                .WithDescription("Enable or disable features for your server")
                .WithColor(new Color(241, 196, 15))
                .WithCurrentTimestamp()
                .WithFooter($"Page {page + 1}/{totalPages} • {toggles.Count} total features");

            embedBuilder.AddField("📊 Summary",
                $"**Total Enabled:** {totalEnabled}/{toggles.Count} features\n" +
                $"**On this page:** {enabledCount}/{pageToggles.Count} enabled", false);

            // Group toggles by status for better organization
            var enabledToggles = pageToggles.Where(t => t.IsEnabled).ToList();
            var disabledToggles = pageToggles.Where(t => !t.IsEnabled).ToList();

            if (enabledToggles.Any())
            {
                embedBuilder.AddField("✅ Enabled Features",
                    string.Join("\n", enabledToggles.Select(t => $"• **{FormatToggleName(t.Name)}**")),
                    true);
            }

            if (disabledToggles.Any())
            {
                embedBuilder.AddField("❌ Disabled Features",
                    string.Join("\n", disabledToggles.Select(t => $"• {FormatToggleName(t.Name)}")),
                    true);
            }

            embeds.Add(embedBuilder.Build());
        }

        return embeds;
    }

    private MessageComponent GenerateToggleComponents(List<Models.Toggle> toggles, ulong guildId, int currentPage, int totalPages)
    {
        var builder = new ComponentBuilderV2();

        // Add toggle buttons - up to 4 per row, max 8 total
        var buttonRows = new List<List<ButtonBuilder>>();
        var currentRowButtons = new List<ButtonBuilder>();

        foreach (var toggle in toggles.Take(8)) // Show up to 8 toggles with buttons
        {
            if (currentRowButtons.Count == 4) // Start new row after 4 buttons
            {
                buttonRows.Add(currentRowButtons);
                currentRowButtons = new List<ButtonBuilder>();
            }

            var emoji = toggle.IsEnabled ? "✅" : "❌";
            var style = toggle.IsEnabled ? ButtonStyle.Success : ButtonStyle.Secondary;
            var label = FormatToggleName(toggle.Name);

            // Truncate label if too long
            if (label.Length > 20)
                label = label.Substring(0, 17) + "...";

            currentRowButtons.Add(
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name, guildId.ToString()))
                    .WithLabel($"{emoji} {label}")
                    .WithStyle(style));
        }

        // Add remaining buttons if any
        if (currentRowButtons.Any())
        {
            buttonRows.Add(currentRowButtons);
        }

        // Add all button rows
        foreach (var rowButtons in buttonRows)
        {
            builder.WithActionRow([.. rowButtons]);
        }

        // Add back button on the last row
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        return builder.Build();
    }

    private async Task ShowCompleteConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        // Ensure all toggles are created for this server
        await _toggleService.CreateServerTogglesIfNotExistsAsync(guildId);

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);

        // Use the new ComponentsV2 approach
        var components = await BuildCompleteConfigurationComponentsV2Async(serverMeta, guild);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
    }

    private async Task ExportConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var toggles = await _toggleService.GetTogglesByServerId(guildId);

        var export = new StringBuilder();
        export.AppendLine($"# Server Configuration Export");
        export.AppendLine($"## Server: {guild.Name}");
        export.AppendLine($"## Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        export.AppendLine();

        export.AppendLine("### Server Metadata");
        export.AppendLine($"- **Persona:** {serverMeta?.Persona ?? "Not configured"}");
        export.AppendLine($"- **Primary Channel ID:** {serverMeta?.PrimaryChannelId?.ToString() ?? "Not configured"}");
        export.AppendLine($"- **NSFW Channel ID:** {serverMeta?.NsfwChannelId?.ToString() ?? "Not configured"}");
        export.AppendLine($"- **Preferred Provider:** {serverMeta?.PreferredProvider ?? "Default"}");
        export.AppendLine();

        export.AppendLine("### Feature Toggles");
        foreach (var toggle in toggles.OrderBy(t => t.Name))
        {
            var status = toggle.IsEnabled ? "✅ Enabled" : "❌ Disabled";
            export.AppendLine($"- **{toggle.Name}:** {status}");
            if (!string.IsNullOrWhiteSpace(toggle.Description))
                export.AppendLine($"  - Description: {toggle.Description}");
        }

        // Send as a file
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(export.ToString()));
        await component.Channel.SendFileAsync(stream, $"config_export_{guildId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md",
            $"Configuration export for **{guild.Name}**");

        await component.ModifyOriginalResponseAsync(msg => msg.Content = "✅ Configuration exported successfully!");
    }

    private async Task ShowHelpAsync(SocketMessageComponent component)
    {
        var embedBuilder = new EmbedBuilder()
            .WithTitle("❓ Configuration Help")
            .WithDescription("Learn how to configure your server settings")
            .WithColor(new Color(155, 89, 182))
            .WithCurrentTimestamp();

        embedBuilder.AddField("🎭 Server Persona",
            "The persona defines how the AI assistant behaves in your server. " +
            "A good persona includes the assistant's role, expertise, communication style, and any specific guidelines.",
            false);

        embedBuilder.AddField("💬 Primary Channel",
            "The primary channel is where the bot will be most active. " +
            "This is typically your main chat channel where members interact with the bot.",
            false);

        embedBuilder.AddField("🤖 AI Provider",
            "Choose which AI model provider to use for generating responses. " +
            "Different providers may have different capabilities and response styles.",
            false);

        embedBuilder.AddField("🎛️ Feature Toggles",
            "Enable or disable specific bot features for your server. " +
            "This allows you to customize which functionalities are available to your members.",
            false);

        embedBuilder.AddField("📚 Need More Help?",
            "• Use `/help` for command information\n" +
            "• Visit our [documentation](https://github.com/HueByte/Amiquin/wiki)\n" +
            "• Join our support server for assistance",
            false);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = null;
        });
    }

    private async Task SetPrimaryChannelAsync(SocketMessageComponent component, ulong guildId, ulong channelId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta != null)
        {
            serverMeta.PrimaryChannelId = channelId;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

            var channel = (component.Channel as SocketGuildChannel)?.Guild?.GetTextChannel(channelId);
            await component.ModifyOriginalResponseAsync(msg =>
                msg.Content = $"✅ Primary channel set to {channel?.Mention ?? $"<#{channelId}>"}");

            // Refresh the interface after a short delay
            await Task.Delay(2000);
            await ShowChannelConfigurationAsync(component, guildId);
        }
    }

    private async Task SetProviderAsync(SocketMessageComponent component, ulong guildId, string provider)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta != null)
        {
            // Properly capitalize the provider name
            var normalizedProvider = provider.ToLower() switch
            {
                "openai" => "OpenAI",
                "anthropic" => "Anthropic",
                "gemini" => "Gemini",
                "grok" => "Grok",
                _ => provider
            };

            serverMeta.PreferredProvider = normalizedProvider;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

            await component.ModifyOriginalResponseAsync(msg =>
                msg.Content = $"✅ AI provider set to **{normalizedProvider}**");

            // Refresh the interface after a short delay
            await Task.Delay(2000);
            await ShowProviderConfigurationAsync(component, guildId);
        }
    }

    private async Task ClearSettingAsync(SocketMessageComponent component, ulong guildId, string setting)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta != null)
        {
            switch (setting)
            {
                case "persona":
                    serverMeta.Persona = null!;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "✅ Persona cleared.");
                    await Task.Delay(2000);
                    await ShowPersonaConfigurationAsync(component, guildId);
                    break;

                case "channel":
                    serverMeta.PrimaryChannelId = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "✅ Primary channel cleared.");
                    await Task.Delay(2000);
                    await ShowChannelConfigurationAsync(component, guildId);
                    break;

                case "nsfw_channel":
                    serverMeta.NsfwChannelId = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "✅ NSFW channel cleared.");
                    await Task.Delay(2000);
                    await ShowNsfwChannelConfigurationAsync(component, guildId);
                    break;

                case "provider":
                    serverMeta.PreferredProvider = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await component.ModifyOriginalResponseAsync(msg => msg.Content = "✅ Provider reset to default.");
                    await Task.Delay(2000);
                    await ShowProviderConfigurationAsync(component, guildId);
                    break;
            }
        }
    }

    private async Task ShowDiscordServerInfoAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var embedBuilder = new EmbedBuilder()
            .WithTitle("ℹ️ Discord Server Information")
            .WithDescription($"Details and statistics for **{guild.Name}**")
            .WithColor(new Color(114, 137, 218)) // Discord blue
            .WithThumbnailUrl(guild.IconUrl)
            .WithCurrentTimestamp();

        // Basic server info
        embedBuilder.AddField("📅 Created",
            $"<t:{guild.CreatedAt.ToUnixTimeSeconds()}:F>\n<t:{guild.CreatedAt.ToUnixTimeSeconds()}:R>", true);

        var owner = guild.Owner;
        embedBuilder.AddField("👑 Owner",
            owner != null ? $"{owner.Mention}\n{owner.Username}#{owner.Discriminator}" : "*Not found*", true);

        embedBuilder.AddField("🆔 Server ID", guild.Id.ToString(), true);

        // Member statistics
        var memberCount = guild.MemberCount;
        var onlineCount = guild.Users.Count(u => u.Status != UserStatus.Offline);
        var botCount = guild.Users.Count(u => u.IsBot);
        var humanCount = memberCount - botCount;

        embedBuilder.AddField("👥 Members",
            $"**Total:** {memberCount:N0}\n**Humans:** {humanCount:N0}\n**Bots:** {botCount:N0}", true);

        embedBuilder.AddField("🟢 Online", $"{onlineCount:N0} members", true);

        // Channel statistics
        var textChannels = guild.TextChannels.Count;
        var voiceChannels = guild.VoiceChannels.Count;
        var categories = guild.CategoryChannels.Count;

        embedBuilder.AddField("📺 Channels",
            $"**Text:** {textChannels}\n**Voice:** {voiceChannels}\n**Categories:** {categories}", true);

        // Server features
        var features = guild.Features.ToString().Replace("_", " ").ToLower();
        if (string.IsNullOrEmpty(features) || features == "0")
            features = "*None*";

        embedBuilder.AddField("✨ Features",
            features.Length > 100 ? $"{features[..100]}..." : features, false);

        // Server boost info
        if (guild.PremiumSubscriptionCount > 0)
        {
            embedBuilder.AddField("💎 Boost Level",
                $"Level {guild.PremiumTier} ({guild.PremiumSubscriptionCount} boosts)", true);
        }

        // Verification level
        embedBuilder.AddField("🛡️ Verification", guild.VerificationLevel.ToString(), true);

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowAmiquinMetadataAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var toggles = await _toggleService.GetTogglesByServerId(guildId);

        var embedBuilder = new EmbedBuilder()
            .WithTitle("⚙️ Amiquin Metadata & Configuration")
            .WithDescription($"Bot configuration and AI settings for **{guild.Name}**")
            .WithColor(new Color(88, 101, 242)) // Discord blurple
            .WithThumbnailUrl("https://cdn.discordapp.com/avatars/YOUR_BOT_ID/avatar.png") // Replace with actual bot avatar
            .WithCurrentTimestamp();

        // Section 1: Configured Channels (Components V2 style)
        var channelInfo = new StringBuilder();

        if (serverMeta?.PrimaryChannelId.HasValue == true)
        {
            var primaryChannel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            channelInfo.AppendLine($"**💬 Primary:** {primaryChannel?.Mention ?? "*Not found*"}");
        }
        else
        {
            channelInfo.AppendLine("**💬 Primary:** *Not configured*");
        }

        if (serverMeta?.NsfwChannelId.HasValue == true)
        {
            var nsfwChannel = guild.GetTextChannel(serverMeta.NsfwChannelId.Value);
            channelInfo.AppendLine($"**🔞 NSFW:** {nsfwChannel?.Mention ?? "*Not found*"}");
        }
        else
        {
            channelInfo.AppendLine("**🔞 NSFW:** *Not configured*");
        }

        embedBuilder.AddField("📺 Configured Channels", channelInfo.ToString().TrimEnd(), false);

        // Section 2: AI Related Information
        var aiInfo = new StringBuilder();
        aiInfo.AppendLine($"**🤖 Provider:** {serverMeta?.PreferredProvider ?? "*Using default (OpenAI)*"}");

        // TODO: Add actual message count and token estimates when these services are available
        aiInfo.AppendLine($"**💬 Conversation Messages:** *Data not available*");
        aiInfo.AppendLine($"**🧠 Memory Tokens (Est.):** *Data not available*");

        embedBuilder.AddField("🤖 AI Configuration", aiInfo.ToString().TrimEnd(), false);

        // Feature Toggle Summary
        var enabledToggles = toggles.Where(t => t.IsEnabled).ToList();
        var disabledToggles = toggles.Where(t => !t.IsEnabled).ToList();

        embedBuilder.AddField("🎛️ Feature Summary",
            $"**Enabled:** {enabledToggles.Count}/{toggles.Count}\n" +
            $"**Most Recent:** {toggles.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.Name ?? "*None*"}", true);

        // Server metadata timestamps
        if (serverMeta != null)
        {
            embedBuilder.AddField("📅 Metadata",
                $"**Created:** <t:{((DateTimeOffset)serverMeta.CreatedAt).ToUnixTimeSeconds()}:R>\n" +
                $"**Updated:** <t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:R>", true);
        }

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowSessionContextAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var embedBuilder = new EmbedBuilder()
            .WithTitle("💭 Session Context & Statistics")
            .WithDescription($"Current conversation context and session stats for **{guild.Name}**")
            .WithColor(new Color(46, 204, 113)) // Green
            .WithCurrentTimestamp();

        // TODO: When chat session service is properly accessible, get real data
        // For now, show placeholder information with proper structure

        embedBuilder.AddField("📊 Active Sessions",
            "*Session data not available*\n" +
            "This will show active conversation sessions, participant counts, and session duration.", false);

        embedBuilder.AddField("💬 Message Statistics",
            "*Message statistics not available*\n" +
            "This will show:\n" +
            "• Recent message count\n" +
            "• Average messages per day\n" +
            "• Most active channels", true);

        embedBuilder.AddField("🧠 Context Memory",
            "*Context memory data not available*\n" +
            "This will show:\n" +
            "• Current context size\n" +
            "• Token usage estimates\n" +
            "• Memory optimization status", true);

        embedBuilder.AddField("⏱️ Response Times",
            "*Performance data not available*\n" +
            "This will show average response times and processing stats.", false);

        embedBuilder.AddField("🔗 Recent Interactions",
            "*No recent interaction data available*\n" +
            "This will show the last few interactions with timestamps.", false);

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowPersonaDetailsAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);

        var embedBuilder = new EmbedBuilder()
            .WithTitle("📖 Server Persona Details")
            .WithDescription($"Complete persona configuration for **{guild.Name}**")
            .WithColor(new Color(155, 89, 182)) // Purple
            .WithCurrentTimestamp();

        if (!string.IsNullOrWhiteSpace(serverMeta?.Persona))
        {
            // Show the full persona without truncation in a code block for better formatting
            var persona = serverMeta.Persona;

            // If the persona is very long, we might need to split it across fields
            if (persona.Length <= 1024)
            {
                embedBuilder.AddField("🎭 Current Persona", $"```{persona}```", false);
            }
            else
            {
                // Split into multiple fields if too long for one field
                var chunks = SplitText(persona, 1000); // Leave some room for the code block markers
                for (int i = 0; i < chunks.Count; i++)
                {
                    var fieldName = i == 0 ? "🎭 Current Persona" : $"🎭 Persona (cont. {i + 1})";
                    embedBuilder.AddField(fieldName, $"```{chunks[i]}```", false);
                }
            }

            // Persona statistics
            embedBuilder.AddField("📊 Persona Statistics",
                $"**Length:** {persona.Length:N0} characters\n" +
                $"**Words:** ~{persona.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length:N0}\n" +
                $"**Lines:** {persona.Split('\n').Length:N0}", true);

            // When was it last updated
            embedBuilder.AddField("📅 Last Updated",
                $"<t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:F>\n" +
                $"<t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:R>", true);
        }
        else
        {
            embedBuilder.AddField("🎭 Current Persona",
                "*No persona configured for this server.*\n\n" +
                "A persona helps define how the AI assistant should behave, including:\n" +
                "• Communication style and tone\n" +
                "• Areas of expertise or focus\n" +
                "• Personality traits\n" +
                "• Response patterns", false);

            embedBuilder.AddField("💡 Getting Started",
                "Use the **Set Persona** button to configure how the AI should behave in your server.", false);
        }

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guildId.ToString()))
                    .WithLabel("✏️ Edit Persona")
                    .WithStyle(ButtonStyle.Primary),
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private List<string> SplitText(string text, int maxLength)
    {
        var result = new List<string>();
        var currentPos = 0;

        while (currentPos < text.Length)
        {
            var remainingLength = text.Length - currentPos;
            var chunkLength = Math.Min(maxLength, remainingLength);

            if (remainingLength > maxLength)
            {
                // Try to break at a word boundary
                var lastSpace = text.LastIndexOf(' ', currentPos + maxLength, maxLength);
                if (lastSpace > currentPos)
                {
                    chunkLength = lastSpace - currentPos;
                }
            }

            result.Add(text.Substring(currentPos, chunkLength));
            currentPos += chunkLength;

            // Skip the space if we broke at a word boundary
            if (currentPos < text.Length && text[currentPos] == ' ')
                currentPos++;
        }

        return result;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Length <= maxLength
            ? text
            : $"{text[..maxLength]}...";
    }

    private string FormatToggleName(string toggleName)
    {
        return toggleName
            .Replace("_", " ")
            .Replace("-", " ")
            .Split(' ')
            .Select(word => char.ToUpper(word[0]) + word[1..].ToLower())
            .Aggregate((a, b) => $"{a} {b}");
    }

    private async Task<bool> HandleModalSubmissionAsync(SocketModal modal, ModalContext context)
    {
        try
        {
            if (context.Parameters.Length < 2)
            {
                await modal.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid modal data.");
                return true;
            }

            var modalType = context.Parameters[0];
            var guildId = ulong.Parse(context.Parameters[1]);

            switch (modalType)
            {
                case "persona":
                    var personaInput = modal.Data.Components.FirstOrDefault(c => c.CustomId == "persona_input")?.Value;
                    if (!string.IsNullOrWhiteSpace(personaInput))
                    {
                        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
                        if (serverMeta != null)
                        {
                            serverMeta.Persona = personaInput;
                            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                            var embed = new EmbedBuilder()
                                .WithTitle("✅ Persona Updated Successfully")
                                .WithDescription("The server persona has been updated.")
                                .AddField("New Persona", personaInput.Length > 500 ? $"{personaInput[..500]}..." : personaInput, false)
                                .WithColor(Color.Green)
                                .WithCurrentTimestamp()
                                .Build();

                            // Show success message first
                            await modal.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Content = null;
                                msg.Embed = embed;
                            });

                            // Wait a moment then return to main configuration interface
                            await Task.Delay(3000);
                            var guildChannel = modal.Channel as SocketGuildChannel;
                            var guild = guildChannel?.Guild;
                            if (guild != null)
                            {
                                var mainComponents = await CreateConfigurationInterfaceAsync(guildId, guild);
                                await modal.ModifyOriginalResponseAsync(msg =>
                                {
                                    msg.Components = mainComponents;
                                    msg.Flags = MessageFlags.ComponentsV2;
                                    msg.Embed = null;
                                    msg.Content = null;
                                });
                            }

                            _logger.LogInformation("Persona updated for guild {GuildId} via modal", guildId);
                        }
                    }
                    break;

                default:
                    await modal.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Unknown modal type.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modal submission");
            await modal.ModifyOriginalResponseAsync(msg => msg.Content = "❌ An error occurred while processing your submission.");
            return true;
        }
    }

    private async Task ShowNsfwChannelConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var nsfwChannels = guild.TextChannels
            .Where(c => c.IsNsfw && guild.CurrentUser.GetPermissions(c).SendMessages)
            .OrderBy(c => c.Position)
            .Take(25)
            .ToList();

        var embedBuilder = new EmbedBuilder()
            .WithTitle("🔞 NSFW Channel Configuration")
            .WithDescription("Set the channel for NSFW content (daily galleries, etc.)\n**⚠️ Only NSFW-marked channels are shown**")
            .WithColor(new Color(255, 0, 100))
            .WithCurrentTimestamp();

        if (serverMeta?.NsfwChannelId.HasValue == true)
        {
            var currentChannel = guild.GetTextChannel(serverMeta.NsfwChannelId.Value);
            embedBuilder.AddField("Current NSFW Channel",
                currentChannel != null ? currentChannel.Mention : "*Channel not found*",
                false);
        }
        else
        {
            embedBuilder.AddField("Current NSFW Channel", "*Not configured*", false);
        }

        // Check if Daily NSFW toggle is enabled
        var isDailyNsfwEnabled = await _toggleService.IsEnabledAsync(guildId, Constants.ToggleNames.EnableDailyNSFW);
        var warningText = isDailyNsfwEnabled
            ? "⚠️ Daily NSFW feature is **enabled**. Set a channel to receive daily content."
            : "ℹ️ Daily NSFW feature is **disabled**. Enable it in Feature Toggles to use this channel.";

        embedBuilder.AddField("Daily NSFW Status", warningText, false);

        var builder = new ComponentBuilderV2();

        if (nsfwChannels.Any())
        {
            // Create channel select menu
            var selectMenuOptions = new List<SelectMenuOptionBuilder>();
            foreach (var channel in nsfwChannels)
            {
                var isSelected = serverMeta?.NsfwChannelId == channel.Id;
                var topic = !string.IsNullOrWhiteSpace(channel.Topic)
                    ? (channel.Topic.Length > 50 ? channel.Topic[..50] + "..." : channel.Topic)
                    : "No topic set";
                selectMenuOptions.Add(new SelectMenuOptionBuilder(
                    $"#{channel.Name}",
                    channel.Id.ToString(),
                    topic,
                    emote: new Emoji("🔞"),
                    isDefault: isSelected));
            }

            var selectMenu = new SelectMenuBuilder(
                _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_nsfw_channel", guildId.ToString()),
                selectMenuOptions)
                .WithPlaceholder("Select an NSFW channel...")
                .WithMinValues(1)
                .WithMaxValues(1);

            builder.WithActionRow([selectMenu]);
        }
        else
        {
            embedBuilder.AddField("⚠️ No NSFW Channels Found",
                "No NSFW channels were found where the bot has permissions. Please:\n" +
                "• Create a channel and mark it as NSFW (18+)\n" +
                "• Ensure the bot has permission to send messages",
                false);
        }

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_nsfw_channel", guildId.ToString()))
                .WithLabel("🗑️ Clear Channel")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(serverMeta?.NsfwChannelId == null),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task SetNsfwChannelAsync(SocketMessageComponent component, ulong guildId, ulong channelId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var channel = guild.GetTextChannel(channelId);
        if (channel == null)
        {
            await component.ModifyOriginalResponseAsync(msg => msg.Content = "❌ Channel not found.");
            return;
        }

        // Verify channel is NSFW
        if (!channel.IsNsfw)
        {
            await component.ModifyOriginalResponseAsync(msg =>
                msg.Content = $"❌ {channel.Mention} is not marked as NSFW. Please mark it as 18+ in channel settings.");
            return;
        }

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta != null)
        {
            serverMeta.NsfwChannelId = channelId;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

            await component.ModifyOriginalResponseAsync(msg =>
                msg.Content = $"✅ NSFW channel set to {channel.Mention}");

            // Refresh the interface after a short delay
            await Task.Delay(2000);
            await ShowNsfwChannelConfigurationAsync(component, guildId);
        }
    }
}