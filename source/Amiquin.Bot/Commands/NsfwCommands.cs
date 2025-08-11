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

    [SlashCommand("waifu", "Get a random NSFW waifu image")]
    public async Task WaifuAsync()
    {
        await HandleNsfwRequestAsync("waifu", "Waifu");
    }

    [SlashCommand("neko", "Get a random NSFW neko image")]
    public async Task NekoAsync()
    {
        await HandleNsfwRequestAsync("neko", "Neko");
    }

    [SlashCommand("trap", "Get a random NSFW trap image")]
    public async Task TrapAsync()
    {
        await HandleNsfwRequestAsync("trap", "Trap");
    }

    [SlashCommand("blowjob", "Get a random NSFW blowjob image")]
    public async Task BlowjobAsync()
    {
        await HandleNsfwRequestAsync("blowjob", "Blowjob");
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