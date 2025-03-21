using System.Collections.Concurrent;
using System.Text;
using Amiquin.Core;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Amiquin.Core.Models;
using Amiquin.Core.Services.Chat.Toggle;
using Amiquin.Core.Utilities;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Microsoft.Extensions.Logging;

namespace Amiquin.Bot.Commands;

[Group("admin", "Admin commands")]
[RequireUserPermission(Discord.GuildPermission.ModerateMembers)]
public class AdminCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly ILogger<AdminCommands> _logger;
    private readonly IToggleService _toggleService;
    private const int WORKER_COUNT = 1;
    private const int DELETE_THROTTLE_MS = 500;
    private const int UPDATE_THROTTLE_MS = 1500;
    private const int BAR_WIDTH = 40;

    public AdminCommands(ILogger<AdminCommands> logger, IToggleService toggleService)
    {
        _logger = logger;
        _toggleService = toggleService;
    }

    [SlashCommand("server-toggles", "List all server toggles")]
    [Ephemeral]
    public async Task ServerTogglesAsync(bool useCache = true)
    {
        await _toggleService.CreateServerTogglesIfNotExistsAsync(Context.Guild.Id, useCache);
        var toggles = await _toggleService.GetTogglesByScopeAsync(ToggleScope.Server);
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
        await _toggleService.SetServerToggleAsync(toggleName, isEnabled, Context.Guild.Id, description);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Set {toggleName} to {isEnabled}");
    }

    [SlashCommand("remove-toggle", "Toggle a feature")]
    [Ephemeral]
    public async Task RemoveToggleAsync(string toggleName)
    {
        await _toggleService.RemoveServerToggleAsync(toggleName, Context.Guild.Id);
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"{toggleName} toggle removed");
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