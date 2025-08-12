using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.Fun;
using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands;

/// <summary>
/// Commands for user interactions with GIFs.
/// </summary>
[Group("interact", "Interactive commands with other users")]
public class InteractionCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly IFunService _funService;

    public InteractionCommands(IFunService funService)
    {
        _funService = funService;
    }

    [SlashCommand("bite", "Bite another user")]
    public async Task BiteAsync([Summary("user", "User to bite")] IUser user)
    {
        await HandleInteractionAsync("bite", user, "bites", "🦷");
    }

    [SlashCommand("kiss", "Kiss another user")]
    public async Task KissAsync([Summary("user", "User to kiss")] IUser user)
    {
        await HandleInteractionAsync("kiss", user, "kisses", "💋");
    }

    [SlashCommand("hug", "Hug another user")]
    public async Task HugAsync([Summary("user", "User to hug")] IUser user)
    {
        await HandleInteractionAsync("hug", user, "hugs", "🤗");
    }

    [SlashCommand("slap", "Slap another user")]
    public async Task SlapAsync([Summary("user", "User to slap")] IUser user)
    {
        await HandleInteractionAsync("slap", user, "slaps", "👋");
    }

    [SlashCommand("pat", "Pat another user")]
    public async Task PatAsync([Summary("user", "User to pat")] IUser user)
    {
        await HandleInteractionAsync("pat", user, "pats", "👋");
    }

    [SlashCommand("poke", "Poke another user")]
    public async Task PokeAsync([Summary("user", "User to poke")] IUser user)
    {
        await HandleInteractionAsync("poke", user, "pokes", "👉");
    }

    [SlashCommand("wave", "Wave at another user")]
    public async Task WaveAsync([Summary("user", "User to wave at")] IUser user)
    {
        await HandleInteractionAsync("wave", user, "waves at", "👋");
    }

    [SlashCommand("highfive", "High five another user")]
    public async Task HighFiveAsync([Summary("user", "User to high five")] IUser user)
    {
        await HandleInteractionAsync("highfive", user, "high fives", "🙏");
    }

    /// <summary>
    /// Generic handler for interaction commands.
    /// </summary>
    private async Task HandleInteractionAsync(string interactionType, IUser targetUser, string actionText, string emoji)
    {
        if (targetUser.Id == Context.User.Id)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = $"You can't {interactionType} yourself! {emoji}");
            return;
        }

        try
        {
            var gifUrl = await _funService.GetInteractionGifAsync(interactionType);

            var embed = new EmbedBuilder()
                .WithDescription($"{emoji} **{Context.User.Mention} {actionText} {targetUser.Mention}!** {emoji}")
                .WithColor(Color.Purple);

            if (!string.IsNullOrEmpty(gifUrl))
            {
                embed.WithImageUrl(gifUrl);
            }

            await ModifyOriginalResponseAsync(msg => msg.Embed = embed.Build());
        }
        catch (Exception)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = $"❌ Failed to {interactionType} {targetUser.Mention}. Try again later!");
        }
    }
}