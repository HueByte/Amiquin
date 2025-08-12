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
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
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

    public MainCommands(
        IPersonaChatService chatService,
        IMessageCacheService messageCacheService,
        IChatContextService chatContextService,
        IFunService funService,
        ISleepService sleepService,
        ISessionManagerService sessionManagerService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _chatContextService = chatContextService;
        _funService = funService;
        _sleepService = sleepService;
        _sessionManagerService = sessionManagerService;
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

        // User Embed
        Embed userEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithAuthor(Context.User)
            .WithColor(Color.Teal)
            .Build();

        var botEmbeds = DiscordUtilities.ChunkMessageAsEmbeds(response, (chunk, chunkIndex, chunkCount) =>
        {
            var footerText = hasContext
                ? $"☁️ Using conversation context ☁️ {chunkIndex}/{chunkCount}"
                : $"☁️ Remembering last {messageCount} messages ☁️ {chunkIndex}/{chunkCount}";

            return new EmbedBuilder()
                .WithDescription(chunk)
                .WithAuthor(Context.Client.CurrentUser)
                .WithColor(Color.Purple)
                .WithFooter(footerText)
                .Build();
        }).ToList();


        if (botEmbeds.Count == 1)
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new Embed[] { userEmbed, botEmbeds.First() }; });
            return;
        }
        else
        {
            await ModifyOriginalResponseAsync((msg) => { msg.Embeds = new Embed[] { userEmbed }; });
            foreach (var botEmbed in botEmbeds)
            {
                await Context.Channel.SendMessageAsync(embed: botEmbed);
            }
        }
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

            var embed = new EmbedBuilder()
                .WithTitle("📏 Size Check")
                .WithDescription($"{targetUser.Mention}'s size: **{size} cm** {sizeDescription}")
                .WithColor(Color.Purple)
                .WithFooter("Results are permanent and totally scientific 🧪")
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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

            // Create embed with color information
            var embed = new EmbedBuilder()
                .WithTitle("🎨 Color Information")
                .WithDescription($"**Hex:** #{cleanHex}\n**RGB:** {r}, {g}, {b}\n**HSL:** {h:F0}°, {s:F0}%, {l:F0}%")
                .WithColor(new Color((byte)r, (byte)g, (byte)b))
                .WithImageUrl(imageUrl)
                .WithFooter($"Requested by {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Attachments = new[] { attachment };
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
        try
        {
            // Generate a color theory palette
            var selectedHarmony = harmony ?? (ColorHarmonyType)Random.Shared.Next(0, 7);
            var palette = await _funService.GenerateColorTheoryPaletteAsync(selectedHarmony, baseHue);

            // Generate color images for each color in the palette in parallel
            var colorImages = new List<FileAttachment>();
            var imageUrls = new List<string>();

            var imageGenerationTasks = palette.Colors.Select(async color =>
            {
                try
                {
                    using var colorImage = await _funService.GenerateColorImageAsync(color.Hex);
                    var cleanHex = color.Hex.TrimStart('#').ToUpper();
                    var fileName = $"palette_color_{cleanHex}.png";
                    var imageUrl = $"attachment://{fileName}";

                    var attachment = new FileAttachment(colorImage, fileName);
                    return (success: true, attachment, imageUrl);
                }
                catch
                {
                    // Return failure for this color
                    return (success: false, attachment: (FileAttachment?)null, imageUrl: (string?)null);
                }
            });

            var results = await Task.WhenAll(imageGenerationTasks);

            foreach (var result in results)
            {
                if (result.success && result.attachment.HasValue && result.imageUrl != null)
                {
                    colorImages.Add(result.attachment.Value);
                    imageUrls.Add(result.imageUrl);
                }
            }

            if (colorImages.Count == 0)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to generate color images for palette. Try again later!");
                return;
            }

            // Create embed with palette information
            var firstColorHex = palette.Colors[0].Hex.TrimStart('#');
            var firstColorInt = Convert.ToInt32(firstColorHex, 16);
            var embedBuilder = new EmbedBuilder()
                .WithTitle($"🎨 {palette.Name}")
                .WithDescription($"**Harmony Type:** {selectedHarmony}\n**Base Hue:** {palette.BaseHue:F1}°\n\n**Description:** {palette.Description}")
                .WithColor(new Color((uint)firstColorInt));

            // Add color details as fields
            foreach (var color in palette.Colors)
            {
                var hex = color.Hex.TrimStart('#');
                var colorInt = Convert.ToInt32(hex, 16);
                var r = (colorInt >> 16) & 0xFF;
                var g = (colorInt >> 8) & 0xFF;
                var b = colorInt & 0xFF;
                
                embedBuilder.AddField(
                    color.Name,
                    $"{color.Hex.ToUpper()} • {color.Role}\nRGB({r}, {g}, {b})",
                    inline: true
                );
            }

            embedBuilder.WithFooter($"Generated by {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithTimestamp(palette.CreatedAt);

            var embed = embedBuilder.Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Attachments = colorImages;
            });
        }
        catch
        {
            // Error will be logged by CommandHandlerService
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Failed to generate color palette. Try again later!");
        }
    }

    [SlashCommand("avatar", "Get a user's avatar")]
    public async Task AvatarAsync([Summary("user", "User to get avatar from (defaults to yourself)")] IUser? user = null)
    {
        var targetUser = user ?? Context.User;
        var displayName = targetUser.GlobalName ?? targetUser.Username;
        var avatarUrl = targetUser.GetDisplayAvatarUrl(ImageFormat.Auto, 1024);

        // Create embed with avatar information
        var embed = new EmbedBuilder()
            .WithTitle($"🖼️ {displayName}'s Avatar")
            .WithDescription($"**User ID:** {targetUser.Id}\n**Account Created:** <t:{targetUser.CreatedAt.ToUnixTimeSeconds()}:D>")
            .WithImageUrl(avatarUrl)
            .WithColor(Color.Blue)
            .WithFooter($"Requested by {Context.User.GlobalName ?? Context.User.Username}", Context.User.GetAvatarUrl())
            .WithCurrentTimestamp()
            .Build();

        // Create button for direct link
        var components = new ComponentBuilder()
            .WithButton("Open in Browser", style: ButtonStyle.Link, url: avatarUrl)
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
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

            // Build the embed with the dynamic response
            var embed = new EmbedBuilder()
                .WithTitle("Nacho Delivery! 🌮")
                .WithDescription($"{response}\n\n**Your total nachos given:** {totalNachos}")
                .WithColor(Color.Orange)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
                .WithFooter($"Nacho #{totalNachos} from {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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

            var embed = new EmbedBuilder()
                .WithTitle($"🏆 Nacho Leaderboard")
                .WithDescription($"**Total nachos received:** {totalNachos} 🌮")
                .WithColor(Color.Gold)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl());

            if (!leaderboardFields.Any())
            {
                embed.AddField("No nachos yet!", "Be the first to give me a nacho with `/nacho`! 🌮", false);
            }
            else
            {
                foreach (var field in leaderboardFields.Take(10))
                {
                    embed.AddField(field.Name, field.Value, field.IsInline);
                }
            }

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
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
        var embed = new EmbedBuilder()
            .WithTitle("☁️ Amiquin Bot Information")
            .WithDescription("A modular and extensible Discord bot")
            .WithColor(Color.Blue)
            .WithThumbnailUrl(Context.Client.CurrentUser.GetDisplayAvatarUrl())
            .AddField("Version", assemblyVersion, true)
            .AddField("Bot ID", Context.Client.CurrentUser.Id.ToString(), true)
            .AddField("Created", Context.Client.CurrentUser.CreatedAt.ToString("MMM dd, yyyy"), true)
            .AddField("Servers", Context.Client.Guilds.Count.ToString(), true)
            .AddField("Users", Context.Client.Guilds.Sum(g => g.MemberCount).ToString(), true)
            .AddField("Shards", Context.Client.Shards.Count.ToString(), true)
            .WithFooter($"Requested by {Context.User.GlobalName ?? Context.User.Username}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithImageUrl(amiquinBannerUrl)
            .Build();

        await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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

        var embed = new EmbedBuilder()
            .WithTitle("😴 Going to Sleep")
            .WithDescription("I'm going to take a 5-minute nap. See you in a bit!")
            .WithColor(Color.Purple)
            .AddField("💤 Sleep Duration", "5 minutes", true)
            .AddField("⏰ Wake Up Time", $"<t:{((DateTimeOffset)wakeUpTime).ToUnixTimeSeconds()}:t>", true)
            .WithFooter($"Requested by {Context.User.GlobalName ?? Context.User.Username}")
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed);
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

            var embed = new EmbedBuilder()
                .WithTitle($"💬 Chat Sessions for {Context.Guild.Name}")
                .WithColor(Color.Blue)
                .WithFooter($"Total sessions: {sessions.Count}")
                .WithCurrentTimestamp();

            foreach (var session in sessions.Take(10)) // Limit to 10 sessions for embed space
            {
                var statusIcon = session.IsActive ? "🟢" : "⚪";
                var lastActivity = session.LastActivityAt > DateTime.UtcNow.AddDays(-1)
                    ? $"<t:{((DateTimeOffset)session.LastActivityAt).ToUnixTimeSeconds()}:R>"
                    : session.LastActivityAt.ToString("MMM dd, yyyy");

                embed.AddField(
                    $"{statusIcon} {session.Name}",
                    $"**Messages:** {session.MessageCount} | **Last Activity:** {lastActivity}\n" +
                    $"**Model:** {session.Provider}/{session.Model}",
                    true);
            }

            if (sessions.Count > 10)
            {
                embed.WithDescription($"*Showing first 10 of {sessions.Count} sessions*");
            }

            var components = new ComponentBuilder()
                .WithButton("Switch Session", "session_switch", ButtonStyle.Primary, new Emoji("🔄"))
                .WithButton("Create New", "session_create", ButtonStyle.Success, new Emoji("➕"))
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = components;
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

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Session Created")
                    .WithDescription($"Created and switched to new session: **{newSession.Name}**")
                    .WithColor(Color.Green)
                    .AddField("Session ID", newSession.Id, true)
                    .AddField("Model", $"{newSession.Provider}/{newSession.Model}", true)
                    .WithFooter($"Created by {Context.User.GlobalName ?? Context.User.Username}")
                    .WithCurrentTimestamp()
                    .Build();

                await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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
                    var embed = new EmbedBuilder()
                        .WithTitle("✅ Session Renamed")
                        .WithDescription($"Renamed session from **{activeSession.Name}** to **{newName.Trim()}**")
                        .WithColor(Color.Green)
                        .WithFooter($"Renamed by {Context.User.GlobalName ?? Context.User.Username}")
                        .WithCurrentTimestamp()
                        .Build();

                    await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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
                    var embed = new EmbedBuilder()
                        .WithTitle("✅ Session Deleted")
                        .WithDescription($"Deleted session: **{sessionToDelete.Name}**")
                        .WithColor(Color.Red)
                        .WithFooter($"Deleted by {Context.User.GlobalName ?? Context.User.Username}")
                        .WithCurrentTimestamp()
                        .Build();

                    await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
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
            var selectMenuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Choose a session to switch to...")
                .WithCustomId("session_switch_select")
                .WithMinValues(1)
                .WithMaxValues(1);

            foreach (var session in sessions.Take(25)) // Discord limit for select menu options
            {
                var isActive = session.IsActive;
                var description = $"{session.MessageCount} msgs, {session.Provider}/{session.Model}";
                if (isActive) description = $"🟢 ACTIVE • {description}";

                selectMenuBuilder.AddOption(
                    label: session.Name,
                    value: session.Id,
                    description: description.Length > 100 ? description[..97] + "..." : description,
                    isDefault: isActive
                );
            }

            var embed = new EmbedBuilder()
                .WithTitle("🔄 Switch Chat Session")
                .WithDescription($"**Current session:** {activeSession?.Name ?? "None"}\n\nSelect a session from the dropdown below:")
                .WithColor(Color.Blue)
                .WithFooter($"Total sessions: {sessions.Count}")
                .Build();

            var components = new ComponentBuilder()
                .WithSelectMenu(selectMenuBuilder)
                .WithButton("Cancel", "session_cancel", ButtonStyle.Secondary, new Emoji("❌"))
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed;
                msg.Components = components;
            });
        }
    }
}