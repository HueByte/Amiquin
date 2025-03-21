using System.Text;
using System.Text.Json;
using Amiquin.Bot.Preconditions;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.Voice;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Bot.Commands;

[Group("dev", "Developer commands")]
[RequireRole("NachoSquad")]
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

    public DevCommands(IChatCoreService chatService, IMessageCacheService messageCacheService, IPersonaService personaService, DiscordShardedClient client, IVoiceService voiceService, IVoiceStateManager voiceStateManager, IPersonaChatService personaChatService, IToggleService toggleService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _personaService = personaService;
        _client = client;
        _voiceService = voiceService;
        _voiceStateManager = voiceStateManager;
        _personaChatService = personaChatService;
        _toggleService = toggleService;
    }

    [SlashCommand("system-toggles", "List system toggles")]
    [Ephemeral]
    [RequireTeam]
    public async Task SystemTogglesAsync()
    {
        var toggles = await _toggleService.GetTogglesByScopeAsync(ToggleScope.Global);
        var sb = new StringBuilder();

        sb.AppendLine("```ini");
        foreach (var toggle in toggles)
        {
            sb.AppendLine($"{toggle.Name} = {toggle.IsEnabled}");
        }
        sb.AppendLine("```");

        var embed = new EmbedBuilder()
            .WithTitle("Server Toggles")
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
            .WithDescription(sb.ToString())
            .WithColor(Color.DarkTeal)
            .Build();

        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }

    [SlashCommand("toggle", "Toggle a feature")]
    [Ephemeral]
    [RequireTeam]
    public async Task ToggleAsync(string toggleName, bool isEnabled, string? description = null)
    {
        await _toggleService.SetSystemToggleAsync(toggleName, isEnabled, description);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set {toggleName} to {isEnabled}");
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

        Embed embed = new EmbedBuilder()
            .WithTitle("Voice Debug")
            .WithDescription(data)
            .WithColor(Color.DarkPurple)
            .Build();

        await ModifyOriginalResponseAsync((msg) => msg.Embeds = new[] { embed });
    }

    [SlashCommand("ping-ephemeral", "Pong! (Ephemeral)")]
    [Ephemeral]
    public async Task PingEphemeralAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Pong!");
    }

    [SlashCommand("persona", "Get persona message")]
    public async Task PersonaAsync()
    {
        var personaCoreMessage = await _messageCacheService.GetPersonaCoreMessageAsync();
        var fullPersonaMessage = await _personaService.GetPersonaAsync();

        List<Embed> chunks = new();

        if (personaCoreMessage?.Length > 2048)
        {
            chunks.AddRange(ChunkMessage(personaCoreMessage, "Core Persona"));
        }
        else
        {
            chunks.Add(new EmbedBuilder()
                .WithTitle("Core Persona")
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithColor(Color.DarkTeal)
                .WithDescription(personaCoreMessage)
                .Build());
        }

        if (fullPersonaMessage?.Length > 2048)
        {
            chunks.AddRange(ChunkMessage(fullPersonaMessage, "Computed Persona"));
        }
        else
        {
            chunks.Add(new EmbedBuilder()
                .WithTitle("Computed Persona")
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithColor(Color.DarkPurple)
                .WithDescription(fullPersonaMessage)
                .Build());
        }

        foreach (var chunk in chunks)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Embed = new EmbedBuilder().WithDescription("Sending my persona below").Build());
            await Context.Channel.SendMessageAsync(embed: chunk);
        }
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
            var response = await _personaChatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, $"{Context.User.GlobalName}: {input}");

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
    [RequireTeam]
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

    private Embed[] ChunkMessage(string message, string title)
    {
        var chunkSize = 2048;
        var chunks = StringModifier.Chunkify(message, chunkSize);
        return chunks.Select((msg, i) => new EmbedBuilder()
                .WithTitle(title)
                .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
                .WithColor(Color.DarkTeal)
                .WithDescription(msg)
                .WithFooter($"Part {i + 1}/{chunks.Count}")
                .Build())
            .ToArray();
    }
}