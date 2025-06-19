using System.Collections.Concurrent;
using System.Text;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Services.BotContext;
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Services.ServerMeta;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace Amiquin.Bot.Commands;

[Group("admin", "Admin commands")]
[RequireUserPermission(Discord.GuildPermission.ModerateMembers)]
public class AdminCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly ILogger<AdminCommands> _logger;
    private readonly IServerMetaService _serverMetaService;
    private readonly IToggleService _toggleService;
    private readonly BotContextAccessor _botContextAccessor;
    private const int WORKER_COUNT = 1;
    private const int DELETE_THROTTLE_MS = 500;
    private const int UPDATE_THROTTLE_MS = 1500;
    private const int BAR_WIDTH = 40;

    public AdminCommands(ILogger<AdminCommands> logger, IServerMetaService serverMetaService, IToggleService toggleService, BotContextAccessor botContextAccessor)
    {
        _logger = logger;
        _serverMetaService = serverMetaService;
        _toggleService = toggleService;
        _botContextAccessor = botContextAccessor;
    }

    [SlashCommand("set-server-persona", "Set the server persona")]
    public async Task SetServerPersonaAsync([Summary("persona", "The persona to set for the server")] string persona)
    {
        if (string.IsNullOrWhiteSpace(persona))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a persona to set.");
            return;
        }

        var serverMeta = _botContextAccessor.ServerMeta;
        if (serverMeta is null)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "Server meta not found. Please try again later.");
            return;
        }

        serverMeta.Persona = persona;

        await _serverMetaService.UpdateServerMetaAsync(serverMeta);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set server persona to: {persona}");
    }

    [SlashCommand("say", "Make the bot say something")]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [Ephemeral]
    public async Task SayAsync([Summary("message", "The message to say")] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a message to say.");
            return;
        }

        var response = await Context.Channel.SendMessageAsync(message);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Said: {response.Content}");
    }

    [SlashCommand("embed-say", "Make the bot say something")]
    [RequireBotPermission(GuildPermission.ManageRoles)]
    [Ephemeral]
    public async Task EmbedSayAsync(string title, string thumbnail, [Summary("message", "The message to say")] string message, bool withAuthor = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "You must provide a message to say.");
            return;
        }

        message = message.Replace("\\n", "\n").Trim(); // Add blockquote formatting for new lines

        var embedBuilder = new EmbedBuilder()
            .WithTitle(title)
            .WithThumbnailUrl(thumbnail)
            .WithDescription(message)
            .WithColor(Color.Magenta);

        if (withAuthor)
            embedBuilder.WithAuthor(Context.User.Username, Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl(), Context.User.GetAvatarUrl());

        var embed = embedBuilder.Build();

        var response = await Context.Channel.SendMessageAsync(embed: embed);
        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }

    [SlashCommand("server-toggles", "List all server toggles")]
    [Ephemeral]
    public async Task ServerTogglesAsync()
    {
        var toggles = await _toggleService.GetTogglesByServerId(Context.Guild.Id);
        var sb = new StringBuilder();

        sb.AppendLine("```ini");
        foreach (var toggle in toggles)
        {
            string finalToggleName = string.Join("::", toggle.Name.Split("::").Skip(1));
            sb.AppendLine($"{finalToggleName} = {toggle.IsEnabled}");
        }
        sb.AppendLine("```");

        var embed = new EmbedBuilder()
            .WithTitle("Server Toggles")
            .WithThumbnailUrl(Context.Guild.IconUrl)
            .WithDescription(sb.ToString())
            .WithColor(Color.DarkTeal)
            .Build();

        await ModifyOriginalResponseAsync((msg) => msg.Embed = embed);
    }

    [SlashCommand("toggle", "Toggle a feature")]
    [Ephemeral]
    public async Task ToggleAsync(string toggleName, bool isEnabled, string? description = null)
    {
        await _toggleService.SetServerToggleAsync(Context.Guild.Id, toggleName, isEnabled, description);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set {toggleName} to {isEnabled}");
    }

    [SlashCommand("nuke", "Nuke the channel")]
    public async Task NukeAsync(int messageCount)
    {
        IUserMessage currentMessage = await ModifyOriginalResponseAsync((msg) => msg.Content = "Preparing to nuke...");
        if (messageCount < 1 || messageCount > 100)
        {
            await currentMessage.ModifyAsync((msg) => msg.Content = "Message count must be between 1 and 100");
            return;
        }

        // +1 as we need to include the self message
        var messageBatches = await Context.Channel.GetMessagesAsync(messageCount + 1, CacheMode.AllowDownload, RequestOptions.Default).ToListAsync();

        var messages = messageBatches.SelectMany(x => x).ToList();

        _logger.LogInformation("{iteractionId} | Deleting {MessageCount} messages in [{ChannelId}]", Context.Interaction.Id, messages.Count - 1, Context.Channel.Id);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Deleting {messages.Count - 1} messages");

        ConcurrentBag<Discord.IMessage> messagesBag = [.. messages];
        var workers = Enumerable.Range(0, WORKER_COUNT).Select(async _ =>
        {
            while (messagesBag.TryTake(out var message))
            {
                // Throttle the deletion to avoid rate limiting 
                await Task.Delay(DELETE_THROTTLE_MS);

                if (message.Id == currentMessage.Id)
                {
                    _logger.LogInformation("{iteractionId} | Skipping self message {MessageId} in [{ChannelId}]", Context.Interaction.Id, message.Id, Context.Channel.Id);
                    continue;
                }

                await DeleteMessageAsync(message);
            }
        }).ToList();

        var updater = Task.Run(async () =>
        {
            double previousProgress = 0;
            while (messagesBag.Count > 0)
            {
                var progress = ProgressUtilities.GetCompletionPercentage(messageCount - messagesBag.Count, messageCount);
                if (progress == previousProgress)
                {
                    await Task.Delay(UPDATE_THROTTLE_MS);
                    continue;
                }

                previousProgress = progress;


                var consoleProgressBar = ProgressUtilities.GenerateConsoleProgressBar(progress, BAR_WIDTH);
                var discordProgressBar = ProgressUtilities.GenerateNachoProgressBar(progress, BAR_WIDTH);

                _logger.LogInformation("Nuke progress: {ProgressPercent}% in [{channelId}]\n{bar}", (int)(progress * 100), Context.Channel.Id, consoleProgressBar);

                var calculatedMessage = $"In progress {(int)(progress * 100)}% {Constants.Emoji.SlugParty} *({messageCount - messagesBag.Count - 1}/{messageCount})* \n{discordProgressBar}";
                await currentMessage.ModifyAsync((msg) => msg.Content = calculatedMessage);

                await Task.Delay(UPDATE_THROTTLE_MS);
            }
        });

        await Task.WhenAll(workers);
        await updater;

        await currentMessage.ModifyAsync((msg) => msg.Content = $"Nuke finished | {messages.Count - 1} messages deleted\n[*No nachos were harmed in the making of this nuke*]");
    }

    private async Task DeleteMessageAsync(Discord.IMessage message)
    {
        try
        {
            await message.DeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete message {MessageId}", message.Id);
        }
    }
}