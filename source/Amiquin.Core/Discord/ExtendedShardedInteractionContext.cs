using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Core.Discord;

public class ExtendedShardedInteractionContext : ShardedInteractionContext, IDisposable, IAsyncDisposable
{
    public AsyncServiceScope AsyncScope { get; }

    public ExtendedShardedInteractionContext(DiscordShardedClient client, SocketInteraction interaction, AsyncServiceScope scope)
        : base(client, interaction)
    {
        AsyncScope = scope;
    }

    ~ExtendedShardedInteractionContext() => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) AsyncScope.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        await AsyncScope.DisposeAsync().ConfigureAwait(false);
    }
}