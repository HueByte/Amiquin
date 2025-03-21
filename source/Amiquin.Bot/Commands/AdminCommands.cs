using System.Collections.Concurrent;
using System.Text;
using Amiquin.Core.Attributes;
using Amiquin.Core.DiscordExtensions;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

namespace Amiquin.Bot.Commands;

[Group("admin", "Admin commands")]
[RequireUserPermission(Discord.GuildPermission.ModerateMembers)]
public class AdminCommands : InteractionModuleBase<ExtendedShardedInteractionContext>
{
    private readonly ILogger<AdminCommands> _logger;
    private const int WORKER_COUNT = 2;
    private const int DELETE_THROTTLE_MS = 200;
    private const int UPDATE_THROTTLE_MS = 500;
    private const int BAR_WIDTH = 40;
    public AdminCommands(ILogger<AdminCommands> logger)
    {
        _logger = logger;
    }


    [SlashCommand("nuke", "Nuke the channel")]
    public async Task NukeAsync(int messageCount)
    {
        IUserMessage currentMessage = await ModifyOriginalResponseAsync((msg) => msg.Content = "Preparing to nuke...");
        if (messageCount < 1 || messageCount > 100)
        {
            await ModifyOriginalResponseAsync((msg) => msg.Content = "Message count must be between 1 and 100");
            return;
        }

        // +1 as we need to include the self message
        var messageBatches = await Context.Channel.GetMessagesAsync(messageCount + 1, CacheMode.AllowDownload, RequestOptions.Default).ToListAsync();

        var messages = messageBatches.SelectMany(x => x).ToList();
        await ModifyOriginalResponseAsync((msg) => msg.Content = $"Deleting {messages.Count - 1} messages");

        ConcurrentBag<Discord.IMessage> messagesBag = [.. messages];
        var workers = Enumerable.Range(0, WORKER_COUNT).Select(async _ =>
        {
            while (messagesBag.TryTake(out var message))
            {
                if (message.Id == currentMessage.Id)
                {
                    _logger.LogInformation("{iteractionId} | Skipping self message {MessageId} in [{ChannelId}]", Context.Interaction.Id, message.Id, Context.Channel.Id);
                    continue;
                }

                await DeleteMessageAsync(message);

                // Throttle the deletion to avoid rate limiting 
                await Task.Delay(DELETE_THROTTLE_MS);
            }
        }).ToList();

        var updater = Task.Run(async () =>
        {
            int previousProgress = 0;
            while (messagesBag.Count > 0)
            {
                var progress = (double)(messageCount - messagesBag.Count) / messageCount;
                var filledLength = (int)(progress * BAR_WIDTH);
                var emptyLength = BAR_WIDTH - filledLength;

                if (filledLength == previousProgress)
                {
                    await Task.Delay(UPDATE_THROTTLE_MS);
                    continue;
                }

                previousProgress = filledLength;

                var emptyEmoteAndAmiquin = string.Join(string.Empty, Enumerable.Repeat("<:blank:1352444144752001094>", filledLength - 1)) + "<:amiquinR:1352445227670962246>";
                var nachoquinEmote = string.Join(string.Empty, Enumerable.Repeat("<:nachoquin:1352442298583089264>", emptyLength));
                string progressBar = new StringBuilder()
                    .Append("[")
                    .Append(emptyEmoteAndAmiquin)
                    .Append(nachoquinEmote)
                    .Append("]")
                    .ToString();

                var consoleBar = $"[{new string('=', filledLength)}{new string(' ', emptyLength)}]";
                _logger.LogInformation("Nuke progress: {ProgressPercent}% in [{channelId}] {bar}", (int)(progress * 100), Context.Channel.Id, consoleBar);

                var calculatedMessage = $"In progress... {messageCount - messagesBag.Count - 1}/{messageCount}\n{progressBar}";
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