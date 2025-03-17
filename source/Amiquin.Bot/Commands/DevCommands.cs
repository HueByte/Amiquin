using Amiquin.Core.Attributes;
using Amiquin.Core.Discord;
using Amiquin.Core.Services.Chat;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Persona;
using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands;

[Group("dev", "Developer commands")]
public class DevCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IChatCoreService _chatService;
    private readonly IMessageCacheService _messageCacheService;
    private readonly IPersonaService _personaService;

    public DevCommands(IChatCoreService chatService, IMessageCacheService messageCacheService, IPersonaService personaService)
    {
        _chatService = chatService;
        _messageCacheService = messageCacheService;
        _personaService = personaService;
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

    [SlashCommand("ping", "Pong!")]
    public async Task PingAsync()
    {
        await ModifyOriginalResponseAsync((msg) => msg.Content = "Pong!");
    }
}