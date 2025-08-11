using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Fun;
using Amiquin.Core.Services.Toggle;
using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands;

/// <summary>
/// NSFW commands for mature content (requires server toggle).
/// </summary>
[Group("nsfw", "NSFW content commands (18+ only, requires server toggle)")]
[RequireNsfw]
public class NsfwCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IFunService _funService;
    private readonly IToggleService _toggleService;

    public NsfwCommands(IFunService funService, IToggleService toggleService)
    {
        _funService = funService;
        _toggleService = toggleService;
    }

    // NSFW-only tags
    [SlashCommand("ero", "Get a random erotic image")]
    public async Task EroAsync()
    {
        await HandleNsfwRequestAsync("ero", "Ero");
    }

    [SlashCommand("ass", "Get a random ass-focused image")]
    public async Task AssAsync()
    {
        await HandleNsfwRequestAsync("ass", "Ass");
    }

    [SlashCommand("hentai", "Get a random hentai image")]
    public async Task HentaiAsync()
    {
        await HandleNsfwRequestAsync("hentai", "Hentai");
    }

    [SlashCommand("milf", "Get a random MILF image")]
    public async Task MilfAsync()
    {
        await HandleNsfwRequestAsync("milf", "MILF");
    }

    [SlashCommand("oral", "Get a random oral image")]
    public async Task OralAsync()
    {
        await HandleNsfwRequestAsync("oral", "Oral");
    }

    [SlashCommand("paizuri", "Get a random paizuri image")]
    public async Task PaizuriAsync()
    {
        await HandleNsfwRequestAsync("paizuri", "Paizuri");
    }

    [SlashCommand("ecchi", "Get a random ecchi image")]
    public async Task EcchiAsync()
    {
        await HandleNsfwRequestAsync("ecchi", "Ecchi");
    }

    // Versatile tags (NSFW versions)
    [SlashCommand("waifu", "Get a random NSFW waifu image")]
    public async Task WaifuAsync()
    {
        await HandleNsfwRequestAsync("waifu", "Waifu");
    }

    [SlashCommand("maid", "Get a random NSFW maid image")]
    public async Task MaidAsync()
    {
        await HandleNsfwRequestAsync("maid", "Maid");
    }

    [SlashCommand("oppai", "Get a random NSFW oppai image")]
    public async Task OppaiAsync()
    {
        await HandleNsfwRequestAsync("oppai", "Oppai");
    }

    [SlashCommand("selfies", "Get a random NSFW selfie image")]
    public async Task SelfiesAsync()
    {
        await HandleNsfwRequestAsync("selfies", "Selfies");
    }

    [SlashCommand("uniform", "Get a random NSFW uniform image")]
    public async Task UniformAsync()
    {
        await HandleNsfwRequestAsync("uniform", "Uniform");
    }

    // Character-specific commands
    [SlashCommand("marin", "Get a random NSFW Marin Kitagawa image")]
    public async Task MarinAsync()
    {
        await HandleNsfwRequestAsync("marin-kitagawa", "Marin Kitagawa");
    }

    [SlashCommand("mori", "Get a random NSFW Mori Calliope image")]
    public async Task MoriAsync()
    {
        await HandleNsfwRequestAsync("mori-calliope", "Mori Calliope");
    }

    [SlashCommand("raiden", "Get a random NSFW Raiden Shogun image")]
    public async Task RaidenAsync()
    {
        await HandleNsfwRequestAsync("raiden-shogun", "Raiden Shogun");
    }

    [SlashCommand("ayaka", "Get a random NSFW Kamisato Ayaka image")]
    public async Task AyakaAsync()
    {
        await HandleNsfwRequestAsync("kamisato-ayaka", "Kamisato Ayaka");
    }

    [SlashCommand("toggle", "Check or toggle NSFW content for this server")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ToggleNsfwAsync(
        [Summary("enable", "Enable or disable NSFW content (leave empty to check status)")] bool? enable = null)
    {
        var serverId = Context.Guild?.Id ?? 0;
        
        if (serverId == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in a server!");
            return;
        }

        if (enable.HasValue)
        {
            // Toggle the NSFW setting
            await _toggleService.SetServerToggleAsync(serverId, Core.Constants.ToggleNames.EnableNSFW, enable.Value, 
                "Enable or disable NSFW content in this server");
            
            var status = enable.Value ? "enabled" : "disabled";
            await ModifyOriginalResponseAsync(msg => msg.Content = $"✅ NSFW content has been **{status}** for this server!");
        }
        else
        {
            // Check current status
            var isEnabled = await _funService.IsNsfwEnabledAsync(serverId);
            var status = isEnabled ? "enabled" : "disabled";
            
            var embed = new EmbedBuilder()
                .WithTitle("NSFW Status")
                .WithDescription($"NSFW content is currently **{status}** for this server.")
                .WithColor(isEnabled ? Color.Red : Color.Green)
                .AddField("How to change", 
                    "Server administrators can use `/nsfw toggle enable:true` or `/nsfw toggle enable:false` to change this setting.")
                .WithCurrentTimestamp()
                .Build();
            
            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
    }

    /// <summary>
    /// Generic handler for NSFW content requests.
    /// </summary>
    private async Task HandleNsfwRequestAsync(string nsfwType, string displayName)
    {
        var serverId = Context.Guild?.Id ?? 0;
        
        if (serverId == 0)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ NSFW commands can only be used in a server!");
            return;
        }

        // Check if channel is NSFW
        if (Context.Channel is ITextChannel textChannel && !textChannel.IsNsfw)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "❌ This command can only be used in NSFW channels!");
            return;
        }

        // Check if NSFW is enabled for the server
        if (!await _funService.IsNsfwEnabledAsync(serverId))
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                "❌ NSFW content is disabled for this server. An administrator can enable it using `/nsfw toggle enable:true`");
            return;
        }

        try
        {
            var imageUrl = await _funService.GetNsfwGifAsync(serverId, nsfwType);
            
            if (string.IsNullOrEmpty(imageUrl))
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = 
                    $"❌ Could not fetch {displayName} content. Please try again later.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"🔞 {displayName}")
                .WithImageUrl(imageUrl)
                .WithColor(Color.Red)
                .WithFooter($"Requested by {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithCurrentTimestamp()
                .Build();

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed);
        }
        catch
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = 
                $"❌ An error occurred while fetching {displayName} content. Please try again later.");
        }
    }
}