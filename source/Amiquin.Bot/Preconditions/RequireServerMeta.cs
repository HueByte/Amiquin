using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Bot.Preconditions;

public class RequireServerMeta : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
    {
        var serverMetaService = services.GetRequiredService<Core.Services.Meta.IServerMetaService>();
        var serverMeta = await serverMetaService.GetServerMetaAsync(context.Guild.Id);
        if (serverMeta is not null)
        {
            return PreconditionResult.FromSuccess();
        }

        return PreconditionResult.FromError("Server metadata does not exist. Please set it up first.");
    }
}