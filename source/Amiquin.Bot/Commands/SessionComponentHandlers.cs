using Amiquin.Core.Services.ComponentHandler;
using Amiquin.Core.Services.SessionManager;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Bot.Commands;

/// <summary>
/// Component handlers for session management functionality.
/// </summary>
public class SessionComponentHandlers
{
    private readonly ILogger<SessionComponentHandlers> _logger;
    private readonly IComponentHandlerService _componentHandlerService;
    private readonly ISessionManagerService _sessionManagerService;

    public SessionComponentHandlers(
        ILogger<SessionComponentHandlers> logger,
        IComponentHandlerService componentHandlerService,
        ISessionManagerService sessionManagerService)
    {
        _logger = logger;
        _componentHandlerService = componentHandlerService;
        _sessionManagerService = sessionManagerService;

        // Register component handlers
        _componentHandlerService.RegisterHandler("session_switch", HandleSessionSwitchButtonAsync);
        _componentHandlerService.RegisterHandler("session_create", HandleSessionCreateButtonAsync);
        _componentHandlerService.RegisterHandler("session_switch_select", HandleSessionSwitchSelectAsync);
        _componentHandlerService.RegisterHandler("session_cancel", HandleSessionCancelAsync);
    }

    /// <summary>
    /// Handles the session switch button click.
    /// </summary>
    private async Task<bool> HandleSessionSwitchButtonAsync(SocketMessageComponent component, ComponentContext context)
    {
        if (component.GuildId == null)
        {
            await component.RespondAsync("❌ This action can only be used in a server.", ephemeral: true);
            return true;
        }

        var sessions = await _sessionManagerService.GetServerSessionsAsync(component.GuildId.Value);

        if (!sessions.Any())
        {
            await component.RespondAsync("❌ No sessions found.", ephemeral: true);
            return true;
        }

        if (sessions.Count == 1)
        {
            await component.RespondAsync("ℹ️ Only one session exists. Use `/session create` to create more sessions.", ephemeral: true);
            return true;
        }

        await ShowSessionSwitchUI(component, sessions);
        return true;
    }

    /// <summary>
    /// Handles the create session button click.
    /// </summary>
    private async Task<bool> HandleSessionCreateButtonAsync(SocketMessageComponent component, ComponentContext context)
    {
        await component.RespondAsync("To create a new session, use the `/session create <name>` command.", ephemeral: true);
        return true;
    }

    /// <summary>
    /// Handles session selection from dropdown.
    /// </summary>
    private async Task<bool> HandleSessionSwitchSelectAsync(SocketMessageComponent component, ComponentContext context)
    {
        if (component.GuildId == null)
        {
            await component.RespondAsync("❌ This action can only be used in a server.", ephemeral: true);
            return true;
        }

        var selectedSessionId = component.Data.Values.FirstOrDefault();
        if (string.IsNullOrEmpty(selectedSessionId))
        {
            await component.RespondAsync("❌ No session selected.", ephemeral: true);
            return true;
        }

        try
        {
            var success = await _sessionManagerService.SwitchSessionAsync(component.GuildId.Value, selectedSessionId);

            if (success)
            {
                // Get session details for confirmation
                var stats = await _sessionManagerService.GetSessionStatsAsync(selectedSessionId);

                var components = new ComponentBuilderV2()
                    .WithContainer(container =>
                    {
                        container.AddComponent(new SectionBuilder()
                            .AddComponent(new TextDisplayBuilder()
                                .WithContent("# ✅ Session Switched")));
                        container.AddComponent(new SectionBuilder()
                            .AddComponent(new TextDisplayBuilder()
                                .WithContent($"Successfully switched to session: **{stats?.Name ?? "Unknown"}**")));
                        container.AddComponent(new SectionBuilder()
                            .AddComponent(new TextDisplayBuilder()
                                .WithContent($"**Messages:** {stats?.MessageCount ?? 0}\n**Model:** {stats?.Provider}/{stats?.Model}")));
                        container.AddComponent(new SectionBuilder()
                            .AddComponent(new TextDisplayBuilder()
                                .WithContent($"*Switched by {component.User.GlobalName ?? component.User.Username}*")));
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
                await component.RespondAsync("❌ Failed to switch session. Session may no longer exist.", ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error switching session {SessionId} for guild {GuildId}", selectedSessionId, component.GuildId);
            await component.RespondAsync("❌ An error occurred while switching sessions.", ephemeral: true);
        }

        return true;
    }

    /// <summary>
    /// Handles the cancel button click.
    /// </summary>
    private async Task<bool> HandleSessionCancelAsync(SocketMessageComponent component, ComponentContext context)
    {
        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("# ❌ Session Switch Cancelled")));
                container.AddComponent(new SectionBuilder()
                    .AddComponent(new TextDisplayBuilder()
                        .WithContent("Session switching was cancelled.")));
            })
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });

        return true;
    }

    /// <summary>
    /// Shows the session switching UI with dropdown.
    /// </summary>
    private async Task ShowSessionSwitchUI(SocketMessageComponent component, List<Amiquin.Core.Models.ChatSession> sessions)
    {
        var activeSession = sessions.FirstOrDefault(s => s.IsActive);

        var selectOptions = sessions.Take(25).Select(session => // Discord limit for select menu options
        {
            var isActive = session.IsActive;
            var description = $"{session.MessageCount} msgs, {session.Provider}/{session.Model}";
            if (isActive) description = $"🟢 ACTIVE • {description}";

            return new SelectMenuOptionBuilder()
                .WithLabel(session.Name)
                .WithValue(session.Id)
                .WithDescription(description.Length > 100 ? description[..97] + "..." : description)
                .WithDefault(isActive);
        }).ToList();

        // Keep the embed for this UI since it has interactive components mixed with ComponentsV2
        var embed = new EmbedBuilder()
            .WithTitle("🔄 Switch Chat Session")
            .WithDescription($"**Current session:** {activeSession?.Name ?? "None"}\n\nSelect a session from the dropdown below:")
            .WithColor(Color.Blue)
            .WithFooter($"Total sessions: {sessions.Count}")
            .Build();

        var components = new ComponentBuilderV2()
            .WithActionRow([
                new SelectMenuBuilder(
                    customId: "session_switch_select",
                    options: selectOptions,
                    placeholder: "Choose a session to switch to...",
                    minValues: 1,
                    maxValues: 1
                )
            ])
            .WithActionRow([
                new ButtonBuilder()
                    .WithLabel("Cancel")
                    .WithCustomId("session_cancel")
                    .WithStyle(ButtonStyle.Secondary)
                    .WithEmote(new Emoji("❌"))
            ])
            .Build();

        await component.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }
}