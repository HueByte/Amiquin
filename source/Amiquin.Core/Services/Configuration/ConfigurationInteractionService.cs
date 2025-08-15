using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.ErrorHandling;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Modal;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Utilities;
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
    private readonly IInteractionErrorHandlerService _errorHandlerService;

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
        IModalService modalService,
        IInteractionErrorHandlerService errorHandlerService)
    {
        _logger = logger;
        _componentHandler = componentHandler;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _paginationService = paginationService;
        _modalService = modalService;
        _errorHandlerService = errorHandlerService;
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



    private async Task<MessageComponent> BuildCompleteConfigurationComponentsV2Async(Models.ServerMeta serverMeta, SocketGuild guild)
    {
        var toggles = await _toggleService.GetTogglesByServerId(guild.Id);
        var builder = new ComponentBuilderV2();

        builder.WithContainer(container =>
        {
            container.WithAccentColor(new Color(52, 152, 219));

            // Header section
            container.WithTextDisplay($"# 📋 Server Configuration\n## {guild.Name}");

            // Server Persona Section with navigation
            var personaContent = !string.IsNullOrWhiteSpace(serverMeta?.Persona)
                ? $"**🎭 Server Persona**\n```\n{TruncateText(serverMeta.Persona, 200)}\n```\n*Click button below to view full persona or edit*"
                : "**🎭 Server Persona**\n*Not configured - click button below to set up*";

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent(personaContent))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix, "persona", guild.Id.ToString()))
                    .WithLabel("Configure Persona")
                    .WithStyle(ButtonStyle.Primary)
                    .WithEmote(new Emoji("🎭"))));

            // Channel Configuration Section
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

            var channelContent = $"**💬 Channel Configuration**\n" +
                $"**Primary:** {channelText}\n" +
                $"**NSFW:** {nsfwChannelText}";

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent(channelContent))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "channel"))
                    .WithLabel("Configure Channels")
                    .WithStyle(ButtonStyle.Primary)
                    .WithEmote(new Emoji("💬"))));

            // AI Provider Section
            var providerText = !string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider)
                ? serverMeta.PreferredProvider
                : "*Using default (OpenAI)*";

            var providerContent = $"**🤖 AI Provider**\n{providerText}";

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent(providerContent))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "provider"))
                    .WithLabel("Configure Provider")
                    .WithStyle(ButtonStyle.Primary)
                    .WithEmote(new Emoji("🤖"))));

            // Feature Toggles Summary Section
            var enabledToggles = toggles.Where(t => t.IsEnabled).ToList();
            var disabledToggles = toggles.Where(t => !t.IsEnabled).ToList();

            var toggleSummaryContent = $"**🎛️ Feature Toggles**\n" +
                $"**Enabled:** {enabledToggles.Count}/{toggles.Count}\n" +
                $"**Top Features:** {(enabledToggles.Any() ? string.Join(", ", enabledToggles.Take(3).Select(t => FormatToggleName(t.Name))) : "*None enabled*")}";

            if (enabledToggles.Count > 3)
            {
                toggleSummaryContent += $"\n*...and {enabledToggles.Count - 3} more*";
            }

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent(toggleSummaryContent))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "toggles"))
                    .WithLabel("Manage Features")
                    .WithStyle(ButtonStyle.Primary)
                    .WithEmote(new Emoji("🎛️"))));

            // Quick Actions Section (displayed as text only since we have action rows below)
            container.WithTextDisplay("**⚡ Quick Actions**\nRapid configuration options available in the buttons below");

            // Information Sections
            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent("**ℹ️ Server Information**\nView Discord server details and statistics"))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "server_info"))
                    .WithLabel("Server Info")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(new Emoji("ℹ️"))));

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent("**⚙️ Bot Metadata**\nView Amiquin configuration and AI settings"))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "amiquin_metadata"))
                    .WithLabel("Bot Metadata")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(new Emoji("⚙️"))));

            container.AddComponent(new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent("**💭 Session Context**\nView current conversation context and statistics"))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "session_context"))
                    .WithLabel("Session Context")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(new Emoji("💭"))));
        });

        // Interactive Components - Navigation Menu
        var selectMenu = new SelectMenuBuilder()
        .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix))
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

    private async Task<bool> HandleConfigMenuAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            // Extract guild ID from interaction context
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "This command can only be used in a server.");
                return true;
            }

            var selectedValue = component.Data.Values?.FirstOrDefault();

            // Verify user has permission
            var guild = component.User as SocketGuildUser;
            if (guild == null || !guild.GuildPermissions.ModerateMembers)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "You need Moderate Members permission to configure the server.");
                return true;
            }

            switch (selectedValue)
            {
                case "persona":
                    await ShowPersonaConfigurationAsync(component, guildId.Value);
                    break;
                case "channel":
                    await ShowChannelConfigurationAsync(component, guildId.Value);
                    break;
                case "nsfw_channel":
                    await ShowNsfwChannelConfigurationAsync(component, guildId.Value);
                    break;
                case "provider":
                    await ShowProviderConfigurationAsync(component, guildId.Value);
                    break;
                case "toggles":
                    await ShowToggleConfigurationAsync(component, guildId.Value);
                    break;
                case "view_all":
                    await ShowCompleteConfigurationAsync(component, guildId.Value);
                    break;
                case "server_info":
                    await ShowDiscordServerInfoAsync(component, guildId.Value);
                    break;
                case "amiquin_metadata":
                    await ShowAmiquinMetadataAsync(component, guildId.Value);
                    break;
                case "session_context":
                    await ShowSessionContextAsync(component, guildId.Value);
                    break;
                case "persona_details":
                    await ShowPersonaDetailsAsync(component, guildId.Value);
                    break;
                default:
                    await _errorHandlerService.RespondWithErrorAsync(component, "Unknown configuration option.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(component, ex, "Config menu interaction");
            return true;
        }
    }

    private async Task<bool> HandleConfigActionAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 1)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Invalid action data.");
                return true;
            }

            var action = context.Parameters[0];
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "This command can only be used in a server.");
                return true;
            }

            // Handle specific configuration actions
            switch (action)
            {
                case "set_channel":
                    if (component.Data.Values.Any())
                    {
                        var channelId = ulong.Parse(component.Data.Values.First());
                        await SetPrimaryChannelAsync(component, guildId.Value, channelId);
                    }
                    break;

                case "set_nsfw_channel":
                    if (component.Data.Values.Any())
                    {
                        var channelId = ulong.Parse(component.Data.Values.First());
                        await SetNsfwChannelAsync(component, guildId.Value, channelId);
                    }
                    break;
                case "set_provider":
                    if (component.Data.Values.Any())
                    {
                        var provider = component.Data.Values.First();
                        await SetProviderAsync(component, guildId.Value, provider);
                    }
                    break;
                case "clear_persona":
                case "clear_channel":
                case "clear_nsfw_channel":
                case "clear_provider":
                    await ClearSettingAsync(component, guildId.Value, action.Replace("clear_", ""));
                    break;
                default:
                    await DiscordUtilities.SendErrorMessageAsync(component, "Unknown action.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(component, ex, "Config action interaction");
            return true;
        }
    }

    private async Task<bool> HandleQuickSetupAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            if (context.Parameters.Length < 1)
            {
                if (component.HasResponded)
                    await DiscordUtilities.SendErrorMessageAsync(component, "Invalid quick setup data.");
                else
                    await component.RespondAsync("❌ Invalid quick setup data.", ephemeral: true);
                return true;
            }

            var setupType = context.Parameters[0];
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                if (component.HasResponded)
                    await DiscordUtilities.SendErrorMessageAsync(component, "This command can only be used in a server.");
                else
                    await component.RespondAsync("❌ This command can only be used in a server.", ephemeral: true);
                return true;
            }

            switch (setupType)
            {
                case "persona":
                    // Show modal for persona input (NOT deferred - handled specially in EventHandlerService)
                    var personaModal = new ModalBuilder()
                        .WithTitle("Set Server Persona")
                        .WithCustomId(_componentHandler.GenerateCustomId(ModalPrefix, "persona"))
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
                    await ShowChannelConfigurationAsync(component, guildId.Value);
                    break;

                case "nsfw_channel":
                    // Component is already deferred by EventHandlerService
                    await ShowNsfwChannelConfigurationAsync(component, guildId.Value);
                    break;

                case "provider":
                    // Component is already deferred by EventHandlerService
                    await ShowProviderConfigurationAsync(component, guildId.Value);
                    break;

                case "toggles":
                    // Component is already deferred by EventHandlerService
                    await ShowToggleConfigurationAsync(component, guildId.Value);
                    break;

                case "server_info":
                    // Component is already deferred by EventHandlerService
                    await ShowDiscordServerInfoAsync(component, guildId.Value);
                    break;

                case "amiquin_metadata":
                    // Component is already deferred by EventHandlerService
                    await ShowAmiquinMetadataAsync(component, guildId.Value);
                    break;

                case "session_context":
                    // Component is already deferred by EventHandlerService
                    await ShowSessionContextAsync(component, guildId.Value);
                    break;

                default:
                    if (component.HasResponded)
                        await DiscordUtilities.SendErrorMessageAsync(component, "Unknown quick setup option.");
                    else
                        await component.RespondAsync("❌ Unknown quick setup option.", ephemeral: true);
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(component, ex, "Quick setup interaction");
            return true;
        }
    }

    private async Task<bool> HandleToggleAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 1)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Invalid toggle data.");
                return true;
            }

            var toggleName = context.Parameters[0];
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "This command can only be used in a server.");
                return true;
            }

            // Get current toggle state
            var toggles = await _toggleService.GetTogglesByServerId(guildId.Value);
            var toggle = toggles.FirstOrDefault(t => t.Name == toggleName);

            if (toggle != null)
            {
                // Toggle the state
                var newState = !toggle.IsEnabled;
                await _toggleService.SetServerToggleAsync(guildId.Value, toggleName, newState);

                // Return to main configuration interface
                var guild = (component.Channel as SocketGuildChannel)?.Guild;
                if (guild != null)
                {
                    var components = await CreateConfigurationInterfaceAsync(guildId.Value, guild);
                    await component.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                    });
                }
            }
            else
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Toggle not found.");
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(component, ex, "Toggle interaction");
            return true;
        }
    }

    private async Task<bool> HandleNavigationAsync(SocketMessageComponent component, ComponentContext context)
    {
        try
        {
            // Component is already deferred by EventHandlerService

            if (context.Parameters.Length < 1)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "Invalid navigation data.");
                return true;
            }

            var action = context.Parameters[0];
            var guildId = (component.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                await DiscordUtilities.SendErrorMessageAsync(component, "This command can only be used in a server.");
                return true;
            }

            switch (action)
            {
                case "refresh":
                case "back":
                    var guild = (component.Channel as SocketGuildChannel)?.Guild;
                    if (guild != null)
                    {
                        var components = await CreateConfigurationInterfaceAsync(guildId.Value, guild);
                        await component.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Components = components;
                            msg.Flags = MessageFlags.ComponentsV2;
                            msg.Embed = null;
                        });
                    }
                    break;

                case "export":
                    await ExportConfigurationAsync(component, guildId.Value);
                    break;

                case "help":
                    await ShowHelpAsync(component, guildId.Value);
                    break;

                default:
                    await DiscordUtilities.SendErrorMessageAsync(component, "Unknown navigation action.");
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(component, ex, "Navigation interaction");
            return true;
        }
    }

    private async Task ShowPersonaConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);

        var title = "🎭 Server Persona Configuration";
        var description = "Configure how the AI assistant behaves in your server";

        // Prepare content sections
        var currentPersonaText = !string.IsNullOrWhiteSpace(serverMeta?.Persona)
            ? $"```{TruncateText(serverMeta.Persona, 1000)}```"
            : "*Not configured*";

        var tipsContent = "💡 **Tips for Writing a Good Persona**\n" +
            "• Be specific about the assistant's role and expertise\n" +
            "• Include desired communication style and tone\n" +
            "• Mention any specific knowledge areas or restrictions\n" +
            "• Keep it concise but comprehensive (under 2000 characters)";

        var builder = new ComponentBuilderV2();

        // Add action buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona"))
                .WithLabel("✏️ Edit Persona")
                .WithStyle(ButtonStyle.Primary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_persona"))
                .WithLabel("🗑️ Clear Persona")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(string.IsNullOrWhiteSpace(serverMeta?.Persona)),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay($"**Current Persona**\n{currentPersonaText}");

                container.WithTextDisplay(tipsContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
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

        var title = "💬 Primary Channel Configuration";
        var description = "Set the main channel where the bot will be most active";

        var currentChannelContent = "**Current Primary Channel**\n";
        if (serverMeta?.PrimaryChannelId.HasValue == true)
        {
            var currentChannel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            currentChannelContent += currentChannel != null ? currentChannel.Mention : "*Channel not found*";
        }
        else
        {
            currentChannelContent += "*Not configured*";
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
                _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_channel"),
                selectMenuOptions)
                .WithPlaceholder("Select a channel...")
                .WithMinValues(1)
                .WithMaxValues(1);

            builder.WithActionRow([selectMenu]);
        }

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_channel"))
                .WithLabel("🗑️ Clear Channel")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(serverMeta?.PrimaryChannelId == null),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(currentChannelContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    private async Task ShowProviderConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var providers = new[] { "OpenAI", "Anthropic", "Gemini", "Grok" };

        var title = "🤖 AI Provider Configuration";
        var description = "Choose your preferred AI model provider";

        var currentProviderContent = "**Current Provider**\n" +
            (!string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider)
                ? $"**{serverMeta.PreferredProvider}**"
                : "*Using default*");

        var availableProvidersContent = "**Available Providers**\n" +
            string.Join("\n", providers.Select(p => $"• **{p}**"));

        var builder = new ComponentBuilderV2();

        // Create provider select menu
        var selectMenuOptions = new List<SelectMenuOptionBuilder>();
        foreach (var provider in providers)
        {
            var isSelected = serverMeta?.PreferredProvider == provider;
            var providerDescription = provider switch
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
                providerDescription,
                isDefault: isSelected));
        }

        var selectMenu = new SelectMenuBuilder(
            _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_provider"),
            selectMenuOptions)
            .WithPlaceholder("Select a provider...")
            .WithMinValues(1)
            .WithMaxValues(1);

        builder.WithActionRow([selectMenu]);

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_provider"))
                .WithLabel("🔄 Use Default")
                .WithStyle(ButtonStyle.Secondary),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        // Implement proper Components V2 with containers and sections
        var v2Components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithAccentColor(new Color(52, 152, 219));

                // Add main title and description section
                container.WithTextDisplay($"# {title}\n{description}");

                // Add content sections
                container.WithTextDisplay(currentProviderContent);

                container.WithTextDisplay(availableProvidersContent);

                // Add components from the traditional ComponentBuilder
                var builtComponents = builder.Build();
                var actionRows = builtComponents.Components.OfType<ActionRowComponent>();

                foreach (var row in actionRows)
                {
                    foreach (var component in row.Components)
                    {
                        // Create a section with this component as accessory and add descriptive text
                        var componentDescription = component switch
                        {
                            ButtonComponent btn => $"**{btn.Label}**",
                            SelectMenuComponent menu => $"**{menu.Placeholder}**",
                            _ => "Configuration option"
                        };

                        var componentBuilder = ConvertToBuilder(component);
                        if (componentBuilder != null)
                        {
                            container.AddComponent(new SectionBuilder()
                                .AddComponent(new TextDisplayBuilder()
                                    .WithContent(componentDescription))
                                .WithAccessory(componentBuilder));
                        }
                    }
                }

                // Ensure we have at least one section if none were added
                if (!actionRows.Any() || !actionRows.SelectMany(r => r.Components).Any())
                {
                    container.WithTextDisplay("Configuration options will appear here.");
                }
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = v2Components;
            msg.Flags = MessageFlags.ComponentsV2;
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
            await DiscordUtilities.SendErrorMessageAsync(component, "Unable to find guild information.");
            return;
        }

        // Generate all pages for pagination
        var pages = GenerateTogglePages(toggles.ToList(), guildId);

        if (pages.Count == 1)
        {
            // Single page - convert embed to ComponentsV2
            var page = pages[0];
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# {page.Title}\n{page.Content}");

                    foreach (var section in page.Sections)
                    {
                        container.WithTextDisplay($"**{section.Title}**\n{section.Content}");
                    }

                    // Add toggle buttons directly to container
                    AddToggleButtonsToContainer(container, toggles.Take(8).ToList(), guildId);
                })
                .Build();

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
        else
        {
            // Multiple pages - use pagination service with ComponentsV2
            var messageComponent = await _paginationService.CreatePaginatedMessageAsync(pages, component.User.Id);

            await component.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = null;
                msg.Components = messageComponent;
                msg.Flags = MessageFlags.ComponentsV2;
            });
        }
    }

    private List<PaginationPage> GenerateTogglePages(List<Models.Toggle> toggles, ulong guildId)
    {
        const int itemsPerPage = 8;
        var pages = new List<PaginationPage>();
        var totalPages = (int)Math.Ceiling((double)toggles.Count / itemsPerPage);
        var totalEnabled = toggles.Count(t => t.IsEnabled);

        for (int page = 0; page < totalPages; page++)
        {
            var startIndex = page * itemsPerPage;
            var pageToggles = toggles.Skip(startIndex).Take(itemsPerPage).ToList();
            var enabledCount = pageToggles.Count(t => t.IsEnabled);

            var paginationPage = new PaginationPage
            {
                Title = "🎛️ Feature Toggles",
                Content = "Enable or disable features for your server",
                Color = new Color(241, 196, 15),
                Timestamp = DateTimeOffset.UtcNow,
                Sections = new List<PageSection>()
            };

            // Add summary section
            paginationPage.Sections.Add(new PageSection
            {
                Title = "📊 Summary",
                Content = $"**Total Enabled:** {totalEnabled}/{toggles.Count} features\n" +
                         $"**On this page:** {enabledCount}/{pageToggles.Count} enabled"
            });

            pages.Add(paginationPage);
        }

        return pages;
    }

    private void AddToggleButtonsToContainer(ContainerBuilder container, List<Models.Toggle> toggles, ulong guildId)
    {
        // Group toggles by enabled/disabled status and create proper sections
        var enabledToggles = toggles.Where(t => t.IsEnabled).ToList();
        var disabledToggles = toggles.Where(t => !t.IsEnabled).ToList();

        // Add individual sections for each enabled toggle
        foreach (var toggle in enabledToggles.Take(10))
        {
            var toggleSection = new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent($"✅ {FormatToggleName(toggle.Name)}"))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name))
                    .WithLabel("Disable")
                    .WithStyle(ButtonStyle.Danger));

            container.AddComponent(toggleSection);
        }

        // Add individual sections for each disabled toggle
        foreach (var toggle in disabledToggles.Take(10))
        {
            var toggleSection = new SectionBuilder()
                .AddComponent(new TextDisplayBuilder()
                    .WithContent($"❌ {FormatToggleName(toggle.Name)}"))
                .WithAccessory(new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name))
                    .WithLabel("Enable")
                    .WithStyle(ButtonStyle.Success));

            container.AddComponent(toggleSection);
        }

        // Add navigation section
        container.AddComponent(new SectionBuilder()
            .AddComponent(new TextDisplayBuilder()
                .WithContent("**Navigation**\nReturn to main configuration menu"))
            .WithAccessory(new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)));
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

        await DiscordUtilities.SendSuccessMessageAsync(component, "Configuration exported successfully!");
    }

    private async Task ShowHelpAsync(SocketMessageComponent component, ulong guildId)
    {
        var title = "❓ Configuration Help";
        var description = "Learn how to configure your server settings";

        var serverPersonaContent = "**🎭 Server Persona**\n" +
            "The persona defines how the AI assistant behaves in your server. " +
            "A good persona includes the assistant's role, expertise, communication style, and any specific guidelines.";

        var primaryChannelContent = "**💬 Primary Channel**\n" +
            "The primary channel is where the bot will be most active. " +
            "This is typically your main chat channel where members interact with the bot.";

        var aiProviderContent = "**🤖 AI Provider**\n" +
            "Choose which AI model provider to use for generating responses. " +
            "Different providers may have different capabilities and response styles.";

        var featureTogglesContent = "**🎛️ Feature Toggles**\n" +
            "Enable or disable specific bot features for your server. " +
            "This allows you to customize which functionalities are available to your members.";

        var needMoreHelpContent = "**📚 Need More Help?**\n" +
            "• Use `/help` for command information\n" +
            "• Visit our [documentation](https://github.com/HueByte/Amiquin/wiki)\n" +
            "• Join our support server for assistance";

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(serverPersonaContent);

                container.WithTextDisplay(primaryChannelContent);

                container.WithTextDisplay(aiProviderContent);

                container.WithTextDisplay(featureTogglesContent);

                container.WithTextDisplay(needMoreHelpContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
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
            await DiscordUtilities.SendSuccessMessageAsync(component, $"Primary channel set to {channel?.Mention ?? $"<#{channelId}>"}");

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

            await DiscordUtilities.SendSuccessMessageAsync(component, $"AI provider set to **{normalizedProvider}**");

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
                    await DiscordUtilities.SendSuccessMessageAsync(component, "Persona cleared.");
                    await Task.Delay(2000);
                    await ShowPersonaConfigurationAsync(component, guildId);
                    break;

                case "channel":
                    serverMeta.PrimaryChannelId = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await DiscordUtilities.SendSuccessMessageAsync(component, "Primary channel cleared.");
                    await Task.Delay(2000);
                    await ShowChannelConfigurationAsync(component, guildId);
                    break;

                case "nsfw_channel":
                    serverMeta.NsfwChannelId = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await DiscordUtilities.SendSuccessMessageAsync(component, "NSFW channel cleared.");
                    await Task.Delay(2000);
                    await ShowNsfwChannelConfigurationAsync(component, guildId);
                    break;

                case "provider":
                    serverMeta.PreferredProvider = null;
                    await _serverMetaService.UpdateServerMetaAsync(serverMeta);
                    await DiscordUtilities.SendSuccessMessageAsync(component, "Provider reset to default.");
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

        var title = "ℹ️ Discord Server Information";
        var description = $"Details and statistics for **{guild.Name}**";

        // Basic server info
        var createdContent = "**📅 Created**\n" +
            $"<t:{guild.CreatedAt.ToUnixTimeSeconds()}:F>\n<t:{guild.CreatedAt.ToUnixTimeSeconds()}:R>";

        var owner = guild.Owner;
        var ownerContent = "**👑 Owner**\n" +
            (owner != null ? $"{owner.Mention}\n{owner.Username}#{owner.Discriminator}" : "*Not found*");

        var serverIdContent = $"**🆔 Server ID**\n{guild.Id}";

        // Member statistics
        var memberCount = guild.MemberCount;
        var onlineCount = guild.Users.Count(u => u.Status != UserStatus.Offline);
        var botCount = guild.Users.Count(u => u.IsBot);
        var humanCount = memberCount - botCount;

        var membersContent = "**👥 Members**\n" +
            $"**Total:** {memberCount:N0}\n**Humans:** {humanCount:N0}\n**Bots:** {botCount:N0}";

        var onlineContent = $"**🟢 Online**\n{onlineCount:N0} members";

        // Channel statistics
        var textChannels = guild.TextChannels.Count;
        var voiceChannels = guild.VoiceChannels.Count;
        var categories = guild.CategoryChannels.Count;

        var channelsContent = "**📺 Channels**\n" +
            $"**Text:** {textChannels}\n**Voice:** {voiceChannels}\n**Categories:** {categories}";

        // Server features
        var features = guild.Features.ToString().Replace("_", " ").ToLower();
        if (string.IsNullOrEmpty(features) || features == "0")
            features = "*None*";

        var featuresContent = "**✨ Features**\n" +
            (features.Length > 100 ? $"{features[..100]}..." : features);

        // Server boost info
        var boostContent = "";
        if (guild.PremiumSubscriptionCount > 0)
        {
            boostContent = "**💎 Boost Level**\n" +
                $"Level {guild.PremiumTier} ({guild.PremiumSubscriptionCount} boosts)";
        }

        // Verification level
        var verificationContent = $"**🛡️ Verification**\n{guild.VerificationLevel}";

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(createdContent);

                container.WithTextDisplay(ownerContent);

                container.WithTextDisplay(serverIdContent);

                container.WithTextDisplay(membersContent);

                container.WithTextDisplay(onlineContent);

                container.WithTextDisplay(channelsContent);

                container.WithTextDisplay(featuresContent);

                if (!string.IsNullOrEmpty(boostContent))
                {
                    container.WithTextDisplay(boostContent);
                }

                container.WithTextDisplay(verificationContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    private async Task ShowAmiquinMetadataAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var toggles = await _toggleService.GetTogglesByServerId(guildId);

        var title = "⚙️ Amiquin Metadata & Configuration";
        var description = $"Bot configuration and AI settings for **{guild.Name}**";

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

        var configuredChannelsContent = $"**📺 Configured Channels**\n{channelInfo.ToString().TrimEnd()}";

        // Section 2: AI Related Information
        var aiInfo = new StringBuilder();
        aiInfo.AppendLine($"**🤖 Provider:** {serverMeta?.PreferredProvider ?? "*Using default (OpenAI)*"}");

        // TODO: Add actual message count and token estimates when these services are available
        aiInfo.AppendLine($"**💬 Conversation Messages:** *Data not available*");
        aiInfo.AppendLine($"**🧠 Memory Tokens (Est.):** *Data not available*");

        var aiConfigContent = $"**🤖 AI Configuration**\n{aiInfo.ToString().TrimEnd()}";

        // Feature Toggle Summary
        var enabledToggles = toggles.Where(t => t.IsEnabled).ToList();
        var disabledToggles = toggles.Where(t => !t.IsEnabled).ToList();

        var featureSummaryContent = $"**🎛️ Feature Summary**\n" +
            $"**Enabled:** {enabledToggles.Count}/{toggles.Count}\n" +
            $"**Most Recent:** {toggles.OrderByDescending(t => t.CreatedAt).FirstOrDefault()?.Name ?? "*None*"}";

        // Server metadata timestamps
        var metadataContent = "";
        if (serverMeta != null)
        {
            metadataContent = $"**📅 Metadata**\n" +
                $"**Created:** <t:{((DateTimeOffset)serverMeta.CreatedAt).ToUnixTimeSeconds()}:R>\n" +
                $"**Updated:** <t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:R>";
        }

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(configuredChannelsContent);

                container.WithTextDisplay(aiConfigContent);

                container.WithTextDisplay(featureSummaryContent);

                if (!string.IsNullOrEmpty(metadataContent))
                {
                    container.WithTextDisplay(metadataContent);
                }

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    private async Task ShowSessionContextAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var title = "💭 Session Context & Statistics";
        var description = $"Current conversation context and session stats for **{guild.Name}**";

        // TODO: When chat session service is properly accessible, get real data
        // For now, show placeholder information with proper structure

        var activeSessionsContent = "**📊 Active Sessions**\n" +
            "*Session data not available*\n" +
            "This will show active conversation sessions, participant counts, and session duration.";

        var messageStatsContent = "**💬 Message Statistics**\n" +
            "*Message statistics not available*\n" +
            "This will show:\n" +
            "• Recent message count\n" +
            "• Average messages per day\n" +
            "• Most active channels";

        var contextMemoryContent = "**🧠 Context Memory**\n" +
            "*Context memory data not available*\n" +
            "This will show:\n" +
            "• Current context size\n" +
            "• Token usage estimates\n" +
            "• Memory optimization status";

        var responseTimesContent = "**⏱️ Response Times**\n" +
            "*Performance data not available*\n" +
            "This will show average response times and processing stats.";

        var recentInteractionsContent = "**🔗 Recent Interactions**\n" +
            "*No recent interaction data available*\n" +
            "This will show the last few interactions with timestamps.";

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(activeSessionsContent);

                container.WithTextDisplay(messageStatsContent);

                container.WithTextDisplay(contextMemoryContent);

                container.WithTextDisplay(responseTimesContent);

                container.WithTextDisplay(recentInteractionsContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    private async Task ShowPersonaDetailsAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);

        var title = "📖 Server Persona Details";
        var description = $"Complete persona configuration for **{guild.Name}**";

        var contentSections = new List<string>();

        if (!string.IsNullOrWhiteSpace(serverMeta?.Persona))
        {
            // Show the full persona without truncation in a code block for better formatting
            var persona = serverMeta.Persona;

            // If the persona is very long, we might need to split it across sections
            if (persona.Length <= 1024)
            {
                contentSections.Add($"**🎭 Current Persona**\n```{persona}```");
            }
            else
            {
                // Split into multiple sections if too long for one section
                var chunks = SplitText(persona, 1000); // Leave some room for the code block markers
                for (int i = 0; i < chunks.Count; i++)
                {
                    var sectionName = i == 0 ? "🎭 Current Persona" : $"🎭 Persona (cont. {i + 1})";
                    contentSections.Add($"**{sectionName}**\n```{chunks[i]}```");
                }
            }

            // Persona statistics
            var personaStatsContent = "**📊 Persona Statistics**\n" +
                $"**Length:** {persona.Length:N0} characters\n" +
                $"**Words:** ~{persona.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length:N0}\n" +
                $"**Lines:** {persona.Split('\n').Length:N0}";
            contentSections.Add(personaStatsContent);

            // When was it last updated
            var lastUpdatedContent = "**📅 Last Updated**\n" +
                $"<t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:F>\n" +
                $"<t:{((DateTimeOffset)serverMeta.LastUpdated).ToUnixTimeSeconds()}:R>";
            contentSections.Add(lastUpdatedContent);
        }
        else
        {
            var noPersonaContent = "**🎭 Current Persona**\n" +
                "*No persona configured for this server.*\n\n" +
                "A persona helps define how the AI assistant should behave, including:\n" +
                "• Communication style and tone\n" +
                "• Areas of expertise or focus\n" +
                "• Personality traits\n" +
                "• Response patterns";
            contentSections.Add(noPersonaContent);

            var gettingStartedContent = "**💡 Getting Started**\n" +
                "Use the **Set Persona** button to configure how the AI should behave in your server.";
            contentSections.Add(gettingStartedContent);
        }

        var builder = new ComponentBuilderV2()
            .WithActionRow([
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona"))
                    .WithLabel("✏️ Edit Persona")
                    .WithStyle(ButtonStyle.Primary),
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                    .WithLabel("← Back")
                    .WithStyle(ButtonStyle.Secondary)
            ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                foreach (var sectionContent in contentSections)
                {
                    container.WithTextDisplay(sectionContent);
                }

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
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
            if (context.Parameters.Length < 1)
            {
                var errorComponents = new ComponentBuilderV2()
                    .WithContainer(container =>
                    {
                        container.WithTextDisplay("# ❌ Invalid Modal Data");
                        container.WithTextDisplay("The modal submission contains invalid data.");
                    })
                    .Build();

                await modal.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = errorComponents;
                    msg.Flags = MessageFlags.ComponentsV2;
                    msg.Embed = null;
                    msg.Content = null;
                });
                return true;
            }

            var modalType = context.Parameters[0];
            var guildId = (modal.Channel as SocketGuildChannel)?.Guild.Id;
            if (guildId == null)
            {
                var errorComponents = new ComponentBuilderV2()
                    .WithContainer(container =>
                    {
                        container.WithTextDisplay("# ❌ Server Required");
                        container.WithTextDisplay("This command can only be used in a server.");
                    })
                    .Build();

                await modal.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = errorComponents;
                    msg.Flags = MessageFlags.ComponentsV2;
                    msg.Embed = null;
                    msg.Content = null;
                });
                return true;
            }

            switch (modalType)
            {
                case "persona":
                    var personaInput = modal.Data.Components.FirstOrDefault(c => c.CustomId == "persona_input")?.Value;
                    if (!string.IsNullOrWhiteSpace(personaInput))
                    {
                        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId.Value);
                        if (serverMeta != null)
                        {
                            serverMeta.Persona = personaInput;
                            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

                            var successTitle = "✅ Persona Updated Successfully";
                            var successDescription = "The server persona has been updated.";
                            var newPersonaContent = $"**New Persona**\n{(personaInput.Length > 500 ? $"{personaInput[..500]}..." : personaInput)}";

                            // Show success message first
                            var successComponents = new ComponentBuilderV2()
                                .WithContainer(container =>
                                {
                                    container.WithTextDisplay($"# {successTitle}\n{successDescription}");

                                    container.WithTextDisplay(newPersonaContent);
                                })
                                .Build();

                            await modal.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Components = successComponents;
                                msg.Flags = MessageFlags.ComponentsV2;
                                msg.Embed = null;
                                msg.Content = null;
                            });

                            // Wait a moment then return to main configuration interface
                            await Task.Delay(3000);
                            var guildChannel = modal.Channel as SocketGuildChannel;
                            var guild = guildChannel?.Guild;
                            if (guild != null)
                            {
                                var mainComponents = await CreateConfigurationInterfaceAsync(guildId.Value, guild);
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
                    var unknownModalComponents = new ComponentBuilderV2()
                        .WithContainer(container =>
                        {
                            container.WithTextDisplay("# ❌ Unknown Modal Type");
                            container.WithTextDisplay("The modal submission type is not recognized.");
                        })
                        .Build();

                    await modal.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = unknownModalComponents;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                        msg.Content = null;
                    });
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            await _errorHandlerService.HandleInteractionErrorAsync(modal, ex, "Modal submission");
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

        var title = "🔞 NSFW Channel Configuration";
        var description = "Set the channel for NSFW content (daily galleries, etc.)\n**⚠️ Only NSFW-marked channels are shown**";

        var currentNsfwChannelContent = "**Current NSFW Channel**\n";
        if (serverMeta?.NsfwChannelId.HasValue == true)
        {
            var currentChannel = guild.GetTextChannel(serverMeta.NsfwChannelId.Value);
            currentNsfwChannelContent += currentChannel != null ? currentChannel.Mention : "*Channel not found*";
        }
        else
        {
            currentNsfwChannelContent += "*Not configured*";
        }

        // Check if Daily NSFW toggle is enabled
        var isDailyNsfwEnabled = await _toggleService.IsEnabledAsync(guildId, Constants.ToggleNames.EnableDailyNSFW);
        var warningText = isDailyNsfwEnabled
            ? "⚠️ Daily NSFW feature is **enabled**. Set a channel to receive daily content."
            : "ℹ️ Daily NSFW feature is **disabled**. Enable it in Feature Toggles to use this channel.";

        var dailyNsfwStatusContent = $"**Daily NSFW Status**\n{warningText}";

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
                _componentHandler.GenerateCustomId(ConfigActionPrefix, "set_nsfw_channel"),
                selectMenuOptions)
                .WithPlaceholder("Select an NSFW channel...")
                .WithMinValues(1)
                .WithMaxValues(1);

            builder.WithActionRow([selectMenu]);
        }

        // Add navigation buttons
        builder.WithActionRow([
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_nsfw_channel"))
                .WithLabel("🗑️ Clear Channel")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(serverMeta?.NsfwChannelId == null),
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary)
        ]);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}\n{description}");

                container.WithTextDisplay(currentNsfwChannelContent);

                container.WithTextDisplay(dailyNsfwStatusContent);

                // Add navigation section
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("**Navigation**\nReturn to main configuration menu"))
                    .WithAccessory(new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back"))
                        .WithLabel("← Back")
                        .WithStyle(ButtonStyle.Secondary)));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    private async Task SetNsfwChannelAsync(SocketMessageComponent component, ulong guildId, ulong channelId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        var channel = guild.GetTextChannel(channelId);
        if (channel == null)
        {
            await DiscordUtilities.SendErrorMessageAsync(component, "Channel not found.");
            return;
        }

        // Verify channel is NSFW
        if (!channel.IsNsfw)
        {
            await DiscordUtilities.SendErrorMessageAsync(component, $"{channel.Mention} is not marked as NSFW.", "Please mark it as 18+ in channel settings.");
            return;
        }

        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        if (serverMeta != null)
        {
            serverMeta.NsfwChannelId = channelId;
            await _serverMetaService.UpdateServerMetaAsync(serverMeta);

            await DiscordUtilities.SendSuccessMessageAsync(component, $"NSFW channel set to {channel.Mention}");

            // Refresh the interface after a short delay
            await Task.Delay(2000);
            await ShowNsfwChannelConfigurationAsync(component, guildId);
        }
    }


    /// <summary>
    /// Converts a Discord component to its builder equivalent for Components V2
    /// Only supports ButtonComponent since SelectMenuComponent cannot be used as section accessory
    /// </summary>
    private static IMessageComponentBuilder? ConvertToBuilder(IMessageComponent component)
    {
        return component switch
        {
            ButtonComponent btn => new ButtonBuilder()
                .WithLabel(btn.Label)
                .WithCustomId(btn.CustomId)
                .WithStyle(btn.Style)
                .WithEmote(btn.Emote)
                .WithUrl(btn.Url)
                .WithDisabled(btn.IsDisabled),
            SelectMenuComponent => null, // SelectMenus cannot be used as section accessories
            _ => throw new ArgumentException($"Unsupported component type: {component.GetType()}")
        };
    }

}