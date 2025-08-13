using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.SessionManager;
using Amiquin.Core.Services.Sleep;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using System.Reflection;
namespace Amiquin.Bot.Commands;

public class MainCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IPersonaChatService _chatService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IChatContextService _chatContextService;
    private readonly IFunService _funService;
    private readonly ISleepService _sleepService;
    private readonly ISessionManagerService _sessionManagerService;
    private readonly ILogger<MainCommands> _logger;

    public MainCommands(
        IPersonaChatService chatService,
        IMessageCacheService messageCacheService,
        IChatContextService chatContextService,
        IFunService funService,
        ISleepService sleepService,
        ISessionManagerService sessionManagerService,
        ILogger<MainCommands> logger)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _chatContextService = chatContextService;
        _funService = funService;
        _sleepService = sleepService;
        _sessionManagerService = sessionManagerService;
        _logger = logger;
    }

    [SlashCommand("chat", "Chat with amiquin!")]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    public async Task ChatAsync(string message)
    {
        message = message.Trim();
        var originalMessage = message;

        // Get context from the ChatContextService
        var context = _chatContextService.FormatContextMessagesForAI(Context.Guild.Id);
        var username = Context.User.GlobalName ?? Context.User.Username;
        var userId = Context.User.Id;

        // Build the prompt with context if available
        var contextPrompt = !string.IsNullOrWhiteSpace(context) ? $"\nRecent context:\n{context}" : "";
        var prompt = $"{contextPrompt}\n[{username}:{userId}] {message}";

        // Send the message with context to the chat service
        var response = await _chatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, prompt);

        // If response is empty, it means the request was silently skipped (duplicate/busy)
        if (string.IsNullOrEmpty(response))
        {
            // Silently delete the deferred response to avoid any message
            await DeleteOriginalResponseAsync();
            return;
        }

        // Track this message in context for future interactions
        // Note: We don't have direct access to SocketMessage here, but we can track the user's input
        // The bot's response will be tracked when it's sent as a regular message

        int messageCount = _messageCacheService.GetChatMessageCount(Context.Guild.Id);
        var hasContext = !string.IsNullOrWhiteSpace(context);

        // Create ComponentsV2 chat response
        var footerText = hasContext
            ? "☁️ Using conversation context ☁️"
            : $"☁️ Remembering last {messageCount} messages ☁️";

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                // User message
                container.WithTextDisplay($"**{Context.User.GlobalName ?? Context.User.Username}**\n{message}");

                // Bot response
                container.WithTextDisplay($"**{Context.Client.CurrentUser.Username}**\n{response}");

                // Footer
                container.WithTextDisplay($"*{footerText}*");
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Embeds = null;
        });
    }

    #region Fun Commands

    [SlashCommand("size", "Check your... size 📏")]
    public async Task SizeAsync([Summary("user", "User to check (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;

        try
        {
            var size = await _funService.GetOrGenerateDickSizeAsync(targetUser.Id, Context.Guild.Id);

            var sizeDescription = size switch
            {
                <= 5 => "🤏 Nano",
                <= 10 => "😬 Small",
                <= 15 => "😐 Average",
                <= 20 => "😎 Above Average",
                <= 25 => "🍆 Large",
                _ => "🐋 MASSIVE"
            };

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay("# 📏 Size Check");
                    container.WithTextDisplay($"{targetUser.Mention}'s size: **{size} cm** {sizeDescription}");
                    container.WithTextDisplay("*Results are permanent and totally scientific 🧪*");
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to measure size. Try again later!");
        }
    }

    [SlashCommand("color", "Display a hex color with ComponentsV2")]
    public async Task ColorAsync([Summary("hex", "Hex color code (e.g., #FF5733 or FF5733)")] string hexColor)
    {
        try
        {
            var cleanHex = hexColor.TrimStart('#').ToUpper();

            // Validate hex color format
            if (cleanHex.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(cleanHex, "^[0-9A-F]+$"))
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid hex color format. Use #RRGGBB or RRGGBB format.");
                return;
            }

            using var colorImage = await _funService.GenerateColorImageAsync($"#{cleanHex}");
            var imageUrl = $"attachment://color_{cleanHex}.png";

            // Parse color for additional information
            var colorValue = uint.Parse(cleanHex, System.Globalization.NumberStyles.HexNumber);
            var r = (colorValue >> 16) & 255;
            var g = (colorValue >> 8) & 255;
            var b = colorValue & 255;

            // Convert RGB to HSL for additional information
            var (h, s, l) = RgbToHsl((byte)r, (byte)g, (byte)b);

            var attachment = new FileAttachment(colorImage, $"color_{cleanHex}.png");

            // Create ComponentsV2 display with color information
            var components = new ComponentBuilderV2()
                .WithTextDisplay($"# 🎨 Color Information\n## #{cleanHex}")
                .WithTextDisplay($"**Hex:** #{cleanHex}\n**RGB:** {r}, {g}, {b}\n**HSL:** {h:F0}°, {s:F0}%, {l:F0}%")
                .WithMediaGallery([imageUrl])
                .WithActionRow([
                    new ButtonBuilder()
                        .WithLabel("🎲 Random Color")
                        .WithCustomId("random_color")
                        .WithStyle(ButtonStyle.Primary),
                    new ButtonBuilder()
                        .WithLabel("🎨 Generate Palette")
                        .WithCustomId($"generate_palette_{cleanHex}")
                        .WithStyle(ButtonStyle.Secondary)
                ])
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Attachments = new[] { attachment };
                msg.Embed = null;
                msg.Content = null;
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid hex color format. Use #RRGGBB or RRGGBB format.");
        }
    }

    /// <summary>
    /// Converts RGB values to HSL.
    /// </summary>
    private static (float h, float s, float l) RgbToHsl(byte red, byte green, byte blue)
    {
        float r = red / 255f;
        float g = green / 255f;
        float b = blue / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));

        float h, s, l = (max + min) / 2;

        if (max == min)
        {
            h = s = 0; // Achromatic
        }
        else
        {
            float d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);

            h = max switch
            {
                _ when max == r => (g - b) / d + (g < b ? 6 : 0),
                _ when max == g => (b - r) / d + 2,
                _ => (r - g) / d + 4,
            };

            h /= 6;
        }

        return (h * 360, s * 100, l * 100);
    }

    [SlashCommand("palette", "Generate a color theory-based palette with visual gallery")]
    public async Task PaletteAsync(
        [Summary("harmony", "Type of color harmony")] ColorHarmonyType? harmony = null,
        [Summary("base_hue", "Base hue (0-360 degrees)")] float? baseHue = null)
    {
        // Generate a color theory palette
        var selectedHarmony = harmony ?? (ColorHarmonyType)Random.Shared.Next(0, 7);
        
        try
        {
            var palette = await _funService.GenerateColorTheoryPaletteAsync(selectedHarmony, baseHue);

            // Create interactive palette with ComponentsV2
            var (components, attachments) = await _funService.CreateInteractivePaletteAsync(palette, Context.User.Id);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Attachments = attachments.ToArray();
                msg.Embed = null;
                msg.Content = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate color palette for user {UserId} with harmony {Harmony} and baseHue {BaseHue}", 
                Context.User.Id, selectedHarmony, baseHue);
            
            await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Failed to generate color palette: {ex.Message}");
        }
    }

    [SlashCommand("avatar", "Get a user's avatar")]
    public async Task AvatarAsync([Summary("user", "User to get avatar from (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var displayName = targetUser.GlobalName ?? targetUser.Username;
        var avatarUrl = targetUser.GetDisplayAvatarUrl(ImageFormat.Auto, 1024);

        // Create ComponentsV2 with avatar information
        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# 🖼️ {displayName}'s Avatar");

                container.WithTextDisplay($"**User ID:** {targetUser.Id}\n**Account Created:** <t:{targetUser.CreatedAt.ToUnixTimeSeconds()}:D>");

                container.WithTextDisplay($"**Avatar:** [View Full Size]({avatarUrl})");

                container.AddComponent(new SectionBuilder()
                    .WithAccessory(new ButtonBuilder()
                        .WithLabel("Open in Browser")
                        .WithStyle(ButtonStyle.Link)
                        .WithUrl(avatarUrl)));

                container.WithTextDisplay($"*Requested by {Context.User.GlobalName ?? Context.User.Username}*");
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
        });
    }

    [SlashCommand("nacho", "Give Amiquin a nacho! 🌮")]
    public async Task NachoAsync()
    {
        try
        {
            // Give the nacho and get the total count
            var totalNachos = await _funService.GiveNachoAsync(Context.User.Id, Context.Guild.Id);

            // Generate a dynamic, context-aware response using AI
            var response = await _funService.GenerateNachoResponseAsync(
                Context.User.Id,
                Context.Guild.Id,
                Context.Channel.Id,
                Context.User.Username,
                totalNachos
            );

            // Build ComponentsV2 with the dynamic response
            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay("# Nacho Delivery! 🌮");

                    container.WithTextDisplay(response);

                    container.WithTextDisplay($"**Your total nachos given:** {totalNachos}");

                    container.WithTextDisplay($"*Nacho #{totalNachos} from {Context.User.Username}*");
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to deliver nacho. Try again later!");
        }
    }

    [SlashCommand("nacho-leaderboard", "View the nacho leaderboard")]
    public async Task NachoLeaderboardAsync()
    {
        try
        {
            var leaderboardFields = await _funService.GetNachoLeaderboardAsync(Context.Guild.Id, 10);
            var totalNachos = await _funService.GetTotalNachosAsync(Context.Guild.Id);

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 🏆 Nacho Leaderboard");

                    container.WithTextDisplay($"**Total nachos received:** {totalNachos} 🌮");

                    if (!leaderboardFields.Any())
                    {
                        container.WithTextDisplay("**No nachos yet!**\nBe the first to give me a nacho with `/nacho`! 🌮");
                    }
                    else
                    {
                        foreach (var field in leaderboardFields.Take(10))
                        {
                            container.WithTextDisplay($"**{field.Name}**\n{field.Value}");
                        }
                    }
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to load nacho leaderboard. Try again later!");
        }
    }

    #endregion

    [SlashCommand("info", "Display bot information including version")]
    public async Task InfoAsync()
    {
        // Get version from assembly
        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
        // var amiquinBannerUrl = $"https://cdn.discordapp.com/banners/{Context.Client.CurrentUser.Id}/{Context.Client.CurrentUser.BannerId}?size=512";

        var amiquinBannerUrl = "https://cdn.discordapp.com/banners/1350616120838590464/ee9ef09c613404439b9fa64ee6cc6a7a?size=512";

        // Create ComponentsV2 display with bot information
        var components = new ComponentBuilderV2()
            .WithTextDisplay("# ☁️ Amiquin Bot Information\n## A modular and extensible Discord bot")
            .WithMediaGallery([amiquinBannerUrl])
            .WithTextDisplay($"**Version:** {assemblyVersion}\n**Bot ID:** {Context.Client.CurrentUser.Id}\n**Created:** {Context.Client.CurrentUser.CreatedAt:MMM dd, yyyy}")
            .WithTextDisplay($"**Servers:** {Context.Client.Guilds.Count}\n**Users:** {Context.Client.Guilds.Sum(g => g.MemberCount):N0}\n**Shards:** {Context.Client.Shards.Count}")
            .WithActionRow([
                new ButtonBuilder()
                    .WithLabel("🔗 GitHub")
                    .WithStyle(ButtonStyle.Link)
                    .WithUrl("https://github.com/HueByte/Amiquin"),
                new ButtonBuilder()
                    .WithLabel("📖 Documentation")
                    .WithStyle(ButtonStyle.Link)
                    .WithUrl("https://github.com/HueByte/Amiquin/wiki"),
                new ButtonBuilder()
                    .WithLabel("💬 Support")
                    .WithCustomId("bot_support")
                    .WithStyle(ButtonStyle.Secondary)
            ])
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
    }

    [SlashCommand("sleep", "Put Amiquin to sleep for 5 minutes")]
    public async Task SleepAsync()
    {
        if (Context.Guild == null)
        {
            await RespondAsync("❌ This command can only be used in a server.", ephemeral: true);
            return;
        }

        // Check if already sleeping
        if (await _sleepService.IsSleepingAsync(Context.Guild.Id))
        {
            var remainingSleep = await _sleepService.GetRemainingSleepTimeAsync(Context.Guild.Id);
            if (remainingSleep.HasValue)
            {
                var minutes = (int)remainingSleep.Value.TotalMinutes + 1; // Round up
                await RespondAsync($"😴 I'm already sleeping! I'll wake up in about **{minutes} minutes**.", ephemeral: true);
                return;
            }
        }

        // Put bot to sleep for 5 minutes
        var wakeUpTime = await _sleepService.PutToSleepAsync(Context.Guild.Id, 5);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay("# 😴 Going to Sleep");

                container.WithTextDisplay("I'm going to take a 5-minute nap. See you in a bit!");

                container.WithTextDisplay($"💤 **Sleep Duration:** 5 minutes\n⏰ **Wake Up Time:** <t:{((DateTimeOffset)wakeUpTime).ToUnixTimeSeconds()}:t>");

                container.WithTextDisplay($"*Requested by {Context.User.GlobalName ?? Context.User.Username}*");
            })
            .Build();

        await RespondAsync(components: components, flags: MessageFlags.ComponentsV2);
    }

    [Group("session", "Manage chat sessions")]
    public class SessionCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
    {
        private readonly ISessionManagerService _sessionManagerService;

        public SessionCommands(ISessionManagerService sessionManagerService)
        {
            _sessionManagerService = sessionManagerService;
        }

        [SlashCommand("list", "View all sessions for this server")]
        public async Task ListSessionsAsync()
        {
            if (Context.Guild == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server.");
                return;
            }

            var sessions = await _sessionManagerService.GetServerSessionsAsync(Context.Guild.Id);
            var activeSession = sessions.FirstOrDefault(s => s.IsActive);

            if (!sessions.Any())
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ No sessions found. This shouldn't happen - creating a default session.");
                return;
            }

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay($"# 💬 Chat Sessions for {Context.Guild.Name}");

                    if (sessions.Count > 10)
                    {
                        container.WithTextDisplay($"*Showing first 10 of {sessions.Count} sessions*");
                    }

                    foreach (var session in sessions.Take(10)) // Limit to 10 sessions
                    {
                        var statusIcon = session.IsActive ? "🟢" : "⚪";
                        var lastActivity = session.LastActivityAt > DateTime.UtcNow.AddDays(-1)
                            ? $"<t:{((DateTimeOffset)session.LastActivityAt).ToUnixTimeSeconds()}:R>"
                            : session.LastActivityAt.ToString("MMM dd, yyyy");

                        container.WithTextDisplay($"{statusIcon} **{session.Name}**\n" +
                                           $"**Messages:** {session.MessageCount} | **Last Activity:** {lastActivity}\n" +
                                           $"**Model:** {session.Provider}/{session.Model}");
                    }

                    container.WithTextDisplay($"*Total sessions: {sessions.Count}*");

                    // Add navigation buttons
                    container.AddComponent(new SectionBuilder()
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Switch Session")
                            .WithCustomId("session_switch")
                            .WithStyle(ButtonStyle.Primary)
                            .WithEmote(new Emoji("🔄")))
                        .WithAccessory(new ButtonBuilder()
                            .WithLabel("Create New")
                            .WithCustomId("session_create")
                            .WithStyle(ButtonStyle.Success)
                            .WithEmote(new Emoji("➕"))));
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }

        [SlashCommand("switch", "Switch to a different session")]
        public async Task SwitchSessionAsync()
        {
            if (Context.Guild == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server.");
                return;
            }

            var sessions = await _sessionManagerService.GetServerSessionsAsync(Context.Guild.Id);

            if (!sessions.Any())
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ No sessions found.");
                return;
            }

            if (sessions.Count == 1)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "ℹ️ Only one session exists. Use `/session create` to create more sessions.");
                return;
            }

            await ShowSessionSwitchUI(sessions);
        }

        [SlashCommand("create", "Create a new chat session")]
        public async Task CreateSessionAsync([Summary("name", "Name for the new session")] string sessionName)
        {
            if (Context.Guild == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server.");
                return;
            }

            if (string.IsNullOrWhiteSpace(sessionName) || sessionName.Length > 50)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Session name must be between 1-50 characters.");
                return;
            }

            try
            {
                var newSession = await _sessionManagerService.CreateSessionAsync(Context.Guild.Id, sessionName.Trim(), setAsActive: true);

                var components = new ComponentBuilderV2()
                    .WithContainer(container =>
                    {
                        container.WithTextDisplay("# ✅ Session Created");

                        container.WithTextDisplay($"Created and switched to new session: **{newSession.Name}**");

                        container.WithTextDisplay($"**Session ID:** {newSession.Id}\n**Model:** {newSession.Provider}/{newSession.Model}");

                        container.WithTextDisplay($"*Created by {Context.User.GlobalName ?? Context.User.Username}*");
                    })
                    .Build();

                await ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = components;
                    msg.Flags = MessageFlags.ComponentsV2;
                    msg.Embed = null;
                });
            }
            catch (InvalidOperationException ex)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ {ex.Message}");
            }
            catch (Exception)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to create session. Try again later.");
            }
        }

        [SlashCommand("rename", "Rename the current active session")]
        public async Task RenameSessionAsync([Summary("name", "New name for the session")] string newName)
        {
            if (Context.Guild == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server.");
                return;
            }

            if (string.IsNullOrWhiteSpace(newName) || newName.Length > 50)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Session name must be between 1-50 characters.");
                return;
            }

            var activeSession = await _sessionManagerService.GetActiveSessionAsync(Context.Guild.Id);
            if (activeSession == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ No active session found.");
                return;
            }

            try
            {
                var success = await _sessionManagerService.RenameSessionAsync(activeSession.Id, newName.Trim());
                if (success)
                {
                    var components = new ComponentBuilderV2()
                        .WithContainer(container =>
                        {
                            container.WithTextDisplay("# ✅ Session Renamed");

                            container.WithTextDisplay($"Renamed session from **{activeSession.Name}** to **{newName.Trim()}**");

                            container.WithTextDisplay($"*Renamed by {Context.User.GlobalName ?? Context.User.Username}*");
                        })
                        .Build();

                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                    });
                }
                else
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to rename session.");
                }
            }
            catch (InvalidOperationException ex)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ {ex.Message}");
            }
            catch (Exception)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to rename session. Try again later.");
            }
        }

        [SlashCommand("delete", "Delete a session (cannot delete the last session)")]
        public async Task DeleteSessionAsync([Summary("name", "Name of the session to delete")] string sessionName)
        {
            if (Context.Guild == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server.");
                return;
            }

            var sessions = await _sessionManagerService.GetServerSessionsAsync(Context.Guild.Id);
            var sessionToDelete = sessions.FirstOrDefault(s => s.Name.Equals(sessionName, StringComparison.OrdinalIgnoreCase));

            if (sessionToDelete == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Session '{sessionName}' not found.");
                return;
            }

            if (sessions.Count <= 1)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Cannot delete the last remaining session.");
                return;
            }

            try
            {
                var success = await _sessionManagerService.DeleteSessionAsync(Context.Guild.Id, sessionToDelete.Id);
                if (success)
                {
                    var components = new ComponentBuilderV2()
                        .WithContainer(container =>
                        {
                            container.WithTextDisplay("# ✅ Session Deleted");

                            container.WithTextDisplay($"Deleted session: **{sessionToDelete.Name}**");

                            container.WithTextDisplay($"*Deleted by {Context.User.GlobalName ?? Context.User.Username}*");
                        })
                        .Build();

                    await ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Components = components;
                        msg.Flags = MessageFlags.ComponentsV2;
                        msg.Embed = null;
                    });
                }
                else
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to delete session.");
                }
            }
            catch (Exception)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to delete session. Try again later.");
            }
        }

        private async Task ShowSessionSwitchUI(List<Amiquin.Core.Models.ChatSession> sessions)
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

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay("# 🔄 Switch Chat Session");

                    container.WithTextDisplay($"**Current session:** {activeSession?.Name ?? "None"}");

                    container.WithTextDisplay("Select a session from the dropdown below:");
                })
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

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
            });
        }
    }
}