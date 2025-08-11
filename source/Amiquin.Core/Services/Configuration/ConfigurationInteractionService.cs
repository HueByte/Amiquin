using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Toggle;
using System.Text;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Modal;

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

    public async Task<(Embed embed, MessageComponent components)> CreateConfigurationInterfaceAsync(ulong guildId, SocketGuild guild)
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

        var embed = await BuildConfigurationEmbedAsync(serverMeta, guild);
        var components = await BuildConfigurationComponentsAsync(serverMeta, guild);

        return (embed, components);
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
        var builder = new ComponentBuilder();

        // Main Navigation Menu
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId(_componentHandler.GenerateCustomId(ConfigMenuPrefix, guild.Id.ToString()))
            .WithPlaceholder("Select a configuration section...")
            .AddOption("Server Persona", "persona", "Configure AI assistant behavior", new Emoji("🎭"))
            .AddOption("Primary Channel", "channel", "Set main bot channel", new Emoji("💬"))
            .AddOption("AI Provider", "provider", "Choose AI model provider", new Emoji("🤖"))
            .AddOption("Feature Toggles", "toggles", "Enable/disable features", new Emoji("🎛️"))
            .AddOption("View All Settings", "view_all", "Show complete configuration", new Emoji("📋"));

        builder.WithSelectMenu(selectMenu);

        // Quick Actions Row
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guild.Id.ToString()))
                .WithLabel("Set Persona")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🎭")),
            row: 1)
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "channel", guild.Id.ToString()))
                .WithLabel("Set Channel")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("💬")),
            row: 1)
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "provider", guild.Id.ToString()))
                .WithLabel("Set Provider")
                .WithStyle(ButtonStyle.Primary)
                .WithEmote(new Emoji("🤖")),
            row: 1);

        // Common Toggles
        var toggles = await _toggleService.GetTogglesByServerId(guild.Id);
        var importantToggles = toggles
            .Where(t => t.Name == "enable_chat" || 
                       t.Name == "enable_voice" ||
                       t.Name == "remember_history")
            .Take(2)
            .ToList();

        if (importantToggles.Any())
        {
            int row = 2;
            foreach (var toggle in importantToggles)
            {
                var emoji = toggle.IsEnabled ? "✅" : "❌";
                var style = toggle.IsEnabled ? ButtonStyle.Success : ButtonStyle.Secondary;
                var label = $"{emoji} {FormatToggleName(toggle.Name)}";
                
                builder.WithButton(
                    new ButtonBuilder()
                        .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name, guild.Id.ToString()))
                        .WithLabel(label)
                        .WithStyle(style),
                    row: row);
            }
        }

        // Footer Actions
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "refresh", guild.Id.ToString()))
                .WithLabel("Refresh")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("🔄")),
            row: 3)
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "export", guild.Id.ToString()))
                .WithLabel("Export")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("📤")),
            row: 3)
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "help", guild.Id.ToString()))
                .WithLabel("Help")
                .WithStyle(ButtonStyle.Secondary)
                .WithEmote(new Emoji("❓")),
            row: 3);

        return builder.Build();
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
                case "provider":
                    await ShowProviderConfigurationAsync(component, guildId);
                    break;
                case "toggles":
                    await ShowToggleConfigurationAsync(component, guildId);
                    break;
                case "view_all":
                    await ShowCompleteConfigurationAsync(component, guildId);
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
                case "set_provider":
                    if (component.Data.Values.Any())
                    {
                        var provider = component.Data.Values.First();
                        await SetProviderAsync(component, guildId, provider);
                    }
                    break;
                case "clear_persona":
                case "clear_channel":
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

                // Refresh the interface
                var guild = (component.Channel as SocketGuildChannel)?.Guild;
                if (guild != null)
                {
                    var (embed, components) = await CreateConfigurationInterfaceAsync(guildId, guild);
                    await component.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embed = embed;
                        msg.Components = components;
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
                        var (embed, components) = await CreateConfigurationInterfaceAsync(guildId, guild);
                        await component.ModifyOriginalResponseAsync(msg =>
                        {
                            msg.Embed = embed;
                            msg.Components = components;
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

        var builder = new ComponentBuilder();
        
        // Add action buttons
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(QuickSetupPrefix, "persona", guildId.ToString()))
                .WithLabel("✏️ Edit Persona")
                .WithStyle(ButtonStyle.Primary))
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_persona", guildId.ToString()))
                .WithLabel("🗑️ Clear Persona")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(string.IsNullOrWhiteSpace(serverMeta?.Persona)))
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary));

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

        var builder = new ComponentBuilder();

        if (textChannels.Any())
        {
            // Create channel select menu
            var selectMenu = new SelectMenuBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "set_channel", guildId.ToString()))
                .WithPlaceholder("Select a channel...")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var channel in textChannels)
            {
                var isSelected = serverMeta?.PrimaryChannelId == channel.Id;
                var topic = channel.Topic != null && channel.Topic.Length > 50 
                    ? channel.Topic[..50] + "..." 
                    : channel.Topic ?? "No topic";
                selectMenu.AddOption(
                    $"#{channel.Name}",
                    channel.Id.ToString(),
                    topic,
                    isDefault: isSelected);
            }

            builder.WithSelectMenu(selectMenu);
        }

        // Add navigation buttons
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_channel", guildId.ToString()))
                .WithLabel("🗑️ Clear Channel")
                .WithStyle(ButtonStyle.Danger)
                .WithDisabled(serverMeta?.PrimaryChannelId == null),
            row: 1)
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary),
            row: 1);

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

        var builder = new ComponentBuilder();

        // Create provider select menu
        var selectMenu = new SelectMenuBuilder()
            .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "set_provider", guildId.ToString()))
            .WithPlaceholder("Select a provider...")
            .WithMinValues(1)
            .WithMaxValues(1);

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
            
            selectMenu.AddOption(provider, provider.ToLower(), description, isDefault: isSelected);
        }

        builder.WithSelectMenu(selectMenu);

        // Add navigation buttons
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(ConfigActionPrefix, "clear_provider", guildId.ToString()))
                .WithLabel("🔄 Use Default")
                .WithStyle(ButtonStyle.Secondary))
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary));

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
        
        var embedBuilder = new EmbedBuilder()
            .WithTitle("🎛️ Feature Toggles")
            .WithDescription("Enable or disable features for your server")
            .WithColor(new Color(241, 196, 15))
            .WithCurrentTimestamp();

        var enabledToggles = toggles.Where(t => t.IsEnabled).ToList();
        var disabledToggles = toggles.Where(t => !t.IsEnabled).ToList();

        if (enabledToggles.Any())
        {
            embedBuilder.AddField("✅ Enabled Features",
                string.Join("\n", enabledToggles.Select(t => $"• {FormatToggleName(t.Name)}").Take(10)),
                false);
        }

        if (disabledToggles.Any())
        {
            embedBuilder.AddField("❌ Disabled Features",
                string.Join("\n", disabledToggles.Select(t => $"• {FormatToggleName(t.Name)}").Take(10)),
                false);
        }

        var builder = new ComponentBuilder();
        
        // Add toggle buttons for first few toggles
        var togglesToShow = toggles.Take(5).ToList();
        int buttonCount = 0;
        foreach (var toggle in togglesToShow)
        {
            if (buttonCount >= 5) break; // Discord limit
            
            var emoji = toggle.IsEnabled ? "✅" : "❌";
            var style = toggle.IsEnabled ? ButtonStyle.Success : ButtonStyle.Secondary;
            
            builder.WithButton(
                new ButtonBuilder()
                    .WithCustomId(_componentHandler.GenerateCustomId(TogglePrefix, toggle.Name, guildId.ToString()))
                    .WithLabel($"{emoji} {FormatToggleName(toggle.Name)}")
                    .WithStyle(style));
            buttonCount++;
        }

        // Add back button
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary),
            row: 1);

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
        });
    }

    private async Task ShowCompleteConfigurationAsync(SocketMessageComponent component, ulong guildId)
    {
        var guild = (component.Channel as SocketGuildChannel)?.Guild;
        if (guild == null) return;

        // Ensure all toggles are created for this server
        await _toggleService.CreateServerTogglesIfNotExistsAsync(guildId);
        
        var serverMeta = await _serverMetaService.GetServerMetaAsync(guildId);
        var toggles = await _toggleService.GetTogglesByServerId(guildId);

        var embedBuilder = new EmbedBuilder()
            .WithTitle("📋 Complete Server Configuration")
            .WithDescription($"All settings for **{guild.Name}**")
            .WithColor(new Color(155, 89, 182))
            .WithThumbnailUrl(guild.IconUrl)
            .WithCurrentTimestamp();

        // Server Meta
        embedBuilder.AddField("🎭 Persona", 
            !string.IsNullOrWhiteSpace(serverMeta?.Persona) 
                ? $"```{TruncateText(serverMeta.Persona, 200)}```" 
                : "*Not configured*", 
            false);

        // Primary Channel
        var channelText = "*Not configured*";
        if (serverMeta?.PrimaryChannelId.HasValue == true)
        {
            var channel = guild.GetTextChannel(serverMeta.PrimaryChannelId.Value);
            channelText = channel != null ? channel.Mention : "*Channel not found*";
        }
        embedBuilder.AddField("💬 Primary Channel", channelText, true);

        // AI Provider
        embedBuilder.AddField("🤖 AI Provider", 
            !string.IsNullOrWhiteSpace(serverMeta?.PreferredProvider) 
                ? serverMeta.PreferredProvider 
                : "*Using default*", 
            true);

        // Feature Toggles
        var enabledToggles = toggles.Where(t => t.IsEnabled).Select(t => FormatToggleName(t.Name));
        var disabledToggles = toggles.Where(t => !t.IsEnabled).Select(t => FormatToggleName(t.Name));

        if (enabledToggles.Any())
        {
            embedBuilder.AddField("✅ Enabled Features", 
                string.Join("\n", enabledToggles.Take(10)), 
                true);
        }

        if (disabledToggles.Any())
        {
            embedBuilder.AddField("❌ Disabled Features", 
                string.Join("\n", disabledToggles.Take(10)), 
                true);
        }

        var builder = new ComponentBuilder();
        
        // Add export and navigation buttons
        builder.WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "export", guildId.ToString()))
                .WithLabel("📤 Export")
                .WithStyle(ButtonStyle.Primary))
        .WithButton(
            new ButtonBuilder()
                .WithCustomId(_componentHandler.GenerateCustomId(NavigationPrefix, "back", guildId.ToString()))
                .WithLabel("← Back")
                .WithStyle(ButtonStyle.Secondary));

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embedBuilder.Build();
            msg.Components = builder.Build();
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

                            await modal.ModifyOriginalResponseAsync(msg =>
                            {
                                msg.Content = null;
                                msg.Embed = embed;
                            });

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
}