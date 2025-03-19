using System.Text.Json;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Persona;
using Amiquin.Core.Services.Voice;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace Amiquin.Bot.Commands;

[Group("dev", "Developer commands")]
public class DevCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IChatCoreService _chatService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IPersonaService _personaService;
    private readonly DiscordShardedClient _client;
    private readonly IVoiceService _voiceService;
    private readonly IVoiceStateManager _voiceStateManager;
    private readonly IPersonaChatService _personaChatService;

    public DevCommands(IChatCoreService chatService, IMessageCacheService messageCacheService, IPersonaService personaService, DiscordShardedClient client, IVoiceService voiceService, IVoiceStateManager voiceStateManager, IPersonaChatService personaChatService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _personaService = personaService;
        _client = client;
        _voiceService = voiceService;
        _voiceStateManager = voiceStateManager;
        _personaChatService = personaChatService;
    }

    [SlashCommand("voicedebug", "debug")]
    [RequireTeam]
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
    [RequireTeam]
    public async Task PersonaAsync()
    {
        var personaCoreMessage = await _messageCacheService.GetPersonaCoreMessage();
        var fullPersonaMessage = await _personaService.GetPersonaAsync();

        var corePersonaEmbed = new EmbedBuilder()
            .WithTitle("Core Persona")
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
            .WithColor(Color.DarkTeal)
            .WithDescription(personaCoreMessage);

        var computedPersonaEmbed = new EmbedBuilder()
            .WithTitle("Computed Persona")
            .WithThumbnailUrl(Context.Client.CurrentUser.GetAvatarUrl())
            .WithColor(Color.DarkPurple)
            .WithDescription(fullPersonaMessage);

        await ModifyOriginalResponseAsync((msg) => msg.Embeds = new[] { corePersonaEmbed.Build(), computedPersonaEmbed.Build() });
    }

    [SlashCommand("join", "Join a voice channel")]
    [RequireTeam]
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
    [RequireTeam]
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

    [SlashCommand("voicechat", "Amiquin will answer in a voice channel")]
    [Ephemeral]
    public async Task VoiceChatAsync(string input)
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
            var response = await _personaChatService.ChatAsync(Context.Guild.Id, Context.User.Id, Context.Client.CurrentUser.Id, $"{Context.User.GlobalName}: {input}");
            await _voiceService.SpeakAsync(voiceChannel, $"Chat listen. {response}");
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = $"Error: {ex.Message}");
            return;
        }

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Oki");
    }

    [SlashCommand("create-nacho-squad", "Create the NachoSquad role")]
    [RequireTeam]
    [Ephemeral]
    public async Task CreateNachoSquad()
    {
        var role = await Context.Guild.CreateRoleAsync("NachoSquad", GuildPermissions.None, Color.Gold, false, true);
        var roleIds = new List<ulong> { role.Id };
        var roleIdsJson = JsonSerializer.Serialize(roleIds);

        await ModifyOriginalResponseAsync((msg) => msg.Content = $"NachoSquad created with ID: {role.Id}");
    }

    [SlashCommand("restart", "Restart the bot")]
    [RequireRole("NachoSquad")]
    [Ephemeral]
    public async Task RestartAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Restarting...");
        Program.Restart();
    }

    [SlashCommand("say", "Amiquin will say something in the voice chat")]
    [RequireRole("NachoSquad")]
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
}