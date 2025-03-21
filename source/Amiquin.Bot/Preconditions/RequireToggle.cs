using Amiquin.Core.Services.Chat.Toggle;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Bot.Preconditions;

public class RequireToggle : PreconditionAttribute
{
    public string ToggleName { get; init; }
    public RequireToggle(string toggleName)
    {
        ToggleName = toggleName;
    }

    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var toggleService = services.GetRequiredService<IToggleService>();
        var serverId = context.Guild.Id;

        return await toggleService.IsEnabledAsync(ToggleName, serverId)
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError("This command is disabled.");
    }
}