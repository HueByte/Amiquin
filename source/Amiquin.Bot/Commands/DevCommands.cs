using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.IRepositories;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.ChatContext;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Pagination;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Services.Voice;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Text.Json;

namespace Amiquin.Bot.Commands;

[Group("dev", "Developer commands")]
[RequireTeam]
public class DevCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IChatCoreService _chatService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IPersonaService _personaService;
    private readonly DiscordShardedClient _client;
    private readonly IVoiceService _voiceService;
    private readonly IVoiceStateManager _voiceStateManager;
    private readonly IPersonaChatService _personaChatService;
    private readonly IToggleService _toggleService;
    private readonly IChatContextService _chatContextService;
    private readonly IPaginationService _paginationService;
    private readonly IChatSessionRepository _chatSessionRepository;
    private readonly IServerMetaService _serverMetaService;

    public DevCommands(IChatCoreService chatService, IMessageCacheService messageCacheService, IPersonaService personaService, DiscordShardedClient client, IVoiceService voiceService, IVoiceStateManager voiceStateManager, IPersonaChatService personaChatService, IToggleService toggleService, IChatContextService chatContextService, IPaginationService paginationService, IChatSessionRepository chatSessionRepository, IServerMetaService serverMetaService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _personaService = personaService;
        _client = client;
        _voiceService = voiceService;
        _voiceStateManager = voiceStateManager;
        _personaChatService = personaChatService;
        _toggleService = toggleService;
        _chatContextService = chatContextService;
        _paginationService = paginationService;
        _chatSessionRepository = chatSessionRepository;
        _serverMetaService = serverMetaService;
    }

    [SlashCommand("toggle-feature", "Toggle a feature")]
    [Ephemeral]
    public async Task ToggleAsync(string toggleName, bool isEnabled, string? description = null)
    {
        await _toggleService.UpdateAllTogglesAsync(toggleName, isEnabled, description);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set {toggleName} to {isEnabled} globally.");
    }

    [SlashCommand("voicedebug", "debug")]
    public async Task VoiceDebugAsync()
    {
        var voiceState = _voiceStateManager.GetAmiquinVoice(Context.Guild.Id);
        if (voiceState is null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "Not in a voice channel");
            return;
        }

        var data = $@"
```vb
Guild: {Context.Guild.Name} | {Context.Guild.Id}
Channel: {voiceState.VoiceChannel?.Name} | {voiceState.VoiceChannel?.Id}
AudioClient State: {voiceState.AudioClient?.ConnectionState}
Latency: {voiceState.AudioClient?.Latency}ms
UDP Latency: {voiceState.AudioClient?.UdpLatency}ms
Streams: {voiceState.AudioClient?.GetStreams().ToDictionary(x => x.Key, x => x.Value.AvailableFrames).Select(x => $"{x.Key}: {x.Value}").Aggregate((x, y) => $"{x}, {y}")}
```
";

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay("# 🔊 Voice Debug");
                container.WithTextDisplay(data);
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
    }

    [SlashCommand("persona", "Get persona message")]
    public async Task PersonaAsync()
    {
        var fullPersonaMessage = await _personaService.GetPersonaAsync(Context.Guild.Id);

        var components = new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay("# 🤖 Computed Persona");
                container.WithTextDisplay($"**Avatar:** [View]({Context.Client.CurrentUser.GetAvatarUrl()})");

                if (fullPersonaMessage?.Length > 3000)
                {
                    container.WithTextDisplay("Persona message is too long. Here's the first part:");
                    container.WithTextDisplay(fullPersonaMessage.Substring(0, 3000) + "...");
                }
                else
                {
                    container.WithTextDisplay(fullPersonaMessage ?? "No persona available");
                }
            })
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = components;
            msg.Flags = MessageFlags.ComponentsV2;
            msg.Embed = null;
            msg.Content = null;
        });
    }

    [SlashCommand("join", "Join a voice channel")]
    public async Task JoinAsync()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel == null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You need to be in a voice channel to use this command.");
            return;
        }

        await _voiceService.JoinAsync(voiceChannel);

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Joined {voiceChannel.Name}");
    }

    [SlashCommand("leave", "Leave a voice channel")]
    public async Task LeaveAsync()
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel == null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You need to be in a voice channel to use this command.");
            return;
        }

        await _voiceService.LeaveAsync(voiceChannel);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Left {voiceChannel.Name}");
    }

    [SlashCommand("voice-chat", "Amiquin will answer in a voice channel")]
    [RequireToggle(Constants.ToggleNames.EnableTTS)]
    [RequireToggle(Constants.ToggleNames.EnableChat)]
    [Ephemeral]
    public async Task VoiceChatAsync(string input)
    {
        input = input.Trim();
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You need to be in a voice channel to use this command.");
            return;
        }

        if (_voiceStateManager.GetAmiquinVoice(Context.Guild.Id) is null)
        {
            await JoinAsync();
        }

        try
        {
            var response = await _personaChatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, $"[{Context.User.GlobalName}:{Context.User.Id}] {input}");

            await ModifyOriginalResponseAsync((msg) => msg.Content = response);
            await _voiceService.SpeakAsync(voiceChannel, $"Chat listen. {response}");
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = $"Error: {ex.Message}");
            return;
        }

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Hm weird, this shouldn't happen...");
    }

    [SlashCommand("create-nacho-squad", "Create the NachoSquad role")]
    [Ephemeral]
    public async Task CreateNachoSquad()
    {
        var role = await Context.Guild.CreateRoleAsync("NachoSquad", GuildPermissions.None, Color.Gold, false, true);
        var roleIds = new List<ulong> { role.Id };
        var roleIdsJson = JsonSerializer.Serialize(roleIds);

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"NachoSquad created with ID: {role.Id}");
    }

    [SlashCommand("restart", "Restart the bot")]
    [Ephemeral]
    public async Task RestartAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Restarting...");
        Program.Restart();
    }

    [SlashCommand("say", "Amiquin will say something in the voice chat")]
    [RequireToggle(Constants.ToggleNames.EnableTTS)]
    [Ephemeral]
    public async Task SaySomethingAsync(string input)
    {
        var voiceChannel = (Context.User as IGuildUser)?.VoiceChannel;
        if (voiceChannel is null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You need to be in a voice channel to use this command.");
            return;
        }

        if (_voiceStateManager.GetAmiquinVoice(Context.Guild.Id) is null)
        {
            await JoinAsync();
        }

        try
        {
            await _voiceService.SpeakAsync(voiceChannel, input);
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = $"Error: {ex.Message}");
            return;
        }

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Oki");
    }

    [SlashCommand("ping", "Pong!")]
    public async Task PingAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Pong!");
    }

    [SlashCommand("debug-conversation", "View current conversation context and session messages")]
    public async Task DebugConversationAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Loading conversation debug data...");

        try
        {
            var guildId = Context.Guild.Id;
            var pages = await CreateDebugPagesAsync(guildId);

            if (pages.Count == 0)
            {
                await ModifyOriginalResponseAsync((msg) => msg.Content = "No conversation data available for this server.");
                return;
            }

            var component = await _paginationService.CreatePaginatedMessageAsync(pages, Context.User.Id);

            await ModifyOriginalResponseAsync((msg) =>
            {
                msg.Content = null;
                msg.Embed = null;
                msg.Components = component;
                msg.Flags = MessageFlags.ComponentsV2;
            });
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = $"Error loading conversation debug data: {ex.Message}");
        }
    }

    private async Task<List<PaginationPage>> CreateDebugPagesAsync(ulong guildId)
    {
        var pages = new List<PaginationPage>();

        // Get context messages from ChatContextService
        var contextMessages = _chatContextService.GetContextMessages(guildId);
        var formattedContext = _chatContextService.FormatContextMessagesForAI(guildId);
        var currentActivity = _chatContextService.GetCurrentActivityLevel(guildId);
        var engagementMultiplier = _chatContextService.GetEngagementMultiplier(guildId);

        // Get conversation session data
        var serverSession = await _chatSessionRepository.GetActiveSessionAsync(SessionScope.Server, serverId: guildId);
        var userSession = await _chatSessionRepository.GetActiveSessionAsync(SessionScope.User, userId: Context.User.Id);
        var channelSession = await _chatSessionRepository.GetActiveSessionAsync(SessionScope.Channel, channelId: Context.Channel.Id);

        // Page 1: Server Information and Statistics
        var serverInfoPage = new PaginationPage
        {
            Title = "🔍 Conversation Debug - Server Information",
            ThumbnailUrl = Context.Guild.IconUrl,
            Color = Color.Blue,
            Timestamp = DateTimeOffset.UtcNow,
            Sections = new List<PageSection>
            {
                new() { Title = "Server Name", Content = Context.Guild.Name, IsInline = true },
                new() { Title = "Server ID", Content = Context.Guild.Id.ToString(), IsInline = true },
                new() { Title = "Member Count", Content = Context.Guild.MemberCount.ToString(), IsInline = true },
                new() { Title = "Current Activity Level", Content = $"{currentActivity:F2}", IsInline = true },
                new() { Title = "Engagement Multiplier", Content = $"{engagementMultiplier:F2}", IsInline = true },
                new() { Title = "Context Messages Count", Content = contextMessages.Length.ToString(), IsInline = true },
                new() { Title = "Server Session", Content = serverSession != null ? $"{serverSession.Model} ({serverSession.Provider})" : "None", IsInline = true },
                new() { Title = "User Session", Content = userSession != null ? $"{userSession.Model} ({userSession.Provider})" : "None", IsInline = true },
                new() { Title = "Channel Session", Content = channelSession != null ? $"{channelSession.Model} ({channelSession.Provider})" : "None", IsInline = true }
            }
        };

        pages.Add(serverInfoPage);

        // Page 2: Raw Context Messages
        if (contextMessages.Length > 0)
        {
            var rawMessagesContent = string.Join("\n", contextMessages.Select((msg, i) => $"{i + 1}. {msg}"));

            if (rawMessagesContent.Length > 4096)
            {
                var chunks = ChunkText(rawMessagesContent, 4000);
                for (int i = 0; i < chunks.Count; i++)
                {
                    var page = new PaginationPage
                    {
                        Title = $"📝 Raw Context Messages (Part {i + 1}/{chunks.Count})",
                        Content = $"```\n{chunks[i]}\n```",
                        Color = Color.Green,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    pages.Add(page);
                }
            }
            else
            {
                var page = new PaginationPage
                {
                    Title = "📝 Raw Context Messages",
                    Content = $"```\n{rawMessagesContent}\n```",
                    Color = Color.Green,
                    Timestamp = DateTimeOffset.UtcNow
                };
                pages.Add(page);
            }
        }
        else
        {
            var page = new PaginationPage
            {
                Title = "📝 Raw Context Messages",
                Content = "No context messages available.",
                Color = Color.Orange,
                Timestamp = DateTimeOffset.UtcNow
            };
            pages.Add(page);
        }

        // Page 3: Formatted AI Context
        if (!string.IsNullOrEmpty(formattedContext))
        {
            if (formattedContext.Length > 4096)
            {
                var chunks = ChunkText(formattedContext, 4000);
                for (int i = 0; i < chunks.Count; i++)
                {
                    var page = new PaginationPage
                    {
                        Title = $"🤖 Formatted AI Context (Part {i + 1}/{chunks.Count})",
                        Content = $"```\n{chunks[i]}\n```",
                        Color = Color.Purple,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    pages.Add(page);
                }
            }
            else
            {
                var page = new PaginationPage
                {
                    Title = "🤖 Formatted AI Context",
                    Content = $"```\n{formattedContext}\n```",
                    Color = Color.Purple,
                    Timestamp = DateTimeOffset.UtcNow
                };
                pages.Add(page);
            }
        }
        else
        {
            var page = new PaginationPage
            {
                Title = "🤖 Formatted AI Context",
                Content = "No formatted context available.",
                Color = Color.Orange,
                Timestamp = DateTimeOffset.UtcNow
            };
            pages.Add(page);
        }

        // Page 4: Conversation Session Messages
        if (serverSession != null && serverSession.Messages.Any())
        {
            var sessionMessages = serverSession.Messages
                .OrderBy(m => m.CreatedAt)
                .Take(20) // Limit to last 20 messages
                .Select((msg, i) => $"{i + 1}. [{msg.Role}] {msg.Content}")
                .ToList();

            var sessionMessagesContent = string.Join("\n", sessionMessages);

            if (sessionMessagesContent.Length > 4096)
            {
                var chunks = ChunkText(sessionMessagesContent, 4000);
                for (int i = 0; i < chunks.Count; i++)
                {
                    var page = new PaginationPage
                    {
                        Title = $"💬 Session Messages (Part {i + 1}/{chunks.Count})",
                        Content = $"```\n{chunks[i]}\n```",
                        Color = Color.Orange,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    pages.Add(page);
                }
            }
            else
            {
                var page = new PaginationPage
                {
                    Title = "💬 Session Messages",
                    Content = $"```\n{sessionMessagesContent}\n```",
                    Color = Color.Orange,
                    Timestamp = DateTimeOffset.UtcNow
                };
                pages.Add(page);
            }
        }
        else
        {
            var page = new PaginationPage
            {
                Title = "💬 Session Messages",
                Content = "No active server session or messages found.",
                Color = Color.Orange,
                Timestamp = DateTimeOffset.UtcNow
            };
            pages.Add(page);
        }

        return pages;
    }

    [SlashCommand("remove-server-meta", "Remove server metadata by server ID")]
    [Ephemeral]
    public async Task RemoveServerMetaAsync(
        [Summary("server-id", "The server ID to remove metadata for")] string serverId)
    {
        try
        {
            if (!ulong.TryParse(serverId, out var serverIdLong))
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "❌ Invalid server ID format.");
                return;
            }

            var serverMeta = await _serverMetaService.GetServerMetaAsync(serverIdLong);
            if (serverMeta == null)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ No server metadata found for server ID: {serverIdLong}");
                return;
            }

            var serverName = serverMeta.ServerName ?? "Unknown";
            await _serverMetaService.DeleteServerMetaAsync(serverIdLong);

            var components = new ComponentBuilderV2()
                .WithContainer(container =>
                {
                    container.WithTextDisplay("# ✅ Server Metadata Removed");
                    container.WithTextDisplay($"Successfully removed server metadata for **{serverName}**");
                    container.WithTextDisplay($"**Server ID:** {serverIdLong}\n**Server Name:** {serverName}");
                })
                .Build();

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Components = components;
                msg.Flags = MessageFlags.ComponentsV2;
                msg.Embed = null;
                msg.Content = null;
            });
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Error removing server metadata: {ex.Message}");
        }
    }

    private List<string> ChunkText(string text, int maxLength)
    {
        var chunks = new List<string>();
        var currentChunk = "";
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            if (currentChunk.Length + line.Length + 1 > maxLength)
            {
                if (!string.IsNullOrEmpty(currentChunk))
                {
                    chunks.Add(currentChunk);
                    currentChunk = "";
                }

                // If a single line is too long, truncate it
                if (line.Length > maxLength)
                {
                    chunks.Add(line.Substring(0, maxLength - 3) + "...");
                }
                else
                {
                    currentChunk = line;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(currentChunk))
                    currentChunk += "\n";
                currentChunk += line;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk))
        {
            chunks.Add(currentChunk);
        }

        return chunks;
    }

    // ChunkMessage method removed - now using ComponentsV2 directly
}