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
        try
        {
            var toggleService = services.GetRequiredService<IToggleService>();
            var serverId = context.Guild.Id;

            var isGlobalToggleEnabled = await toggleService.IsEnabledAsync(ToggleName);
            if (!isGlobalToggleEnabled)
            {
                return PreconditionResult.FromError("This command is disabled globally.");
            }

            return await toggleService.IsEnabledAsync(serverId, ToggleName)
                ? PreconditionResult.FromSuccess()
                : PreconditionResult.FromError("This command is disabled.");
        }
        catch (Exception ex)
        {
            // Log the exception if necessary
            return PreconditionResult.FromError($"An error occurred while checking the toggle: {ex.Message}");
        }
    }
}