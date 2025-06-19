using Amiquin.Core.DiscordExtensions;
using Microsoft.Extensions.Configuration;

namespace Amiquin.Core.Services.BotContext;

public class BotContextAccessor
{
    public string BotName = "Amiquin";
    public string BotVersion = "1.0.0";
    public ulong ServerId => Context?.Guild?.Id ?? 0;
    public Models.ServerMeta? ServerMeta { get; private set; }
    public ExtendedShardedInteractionContext? Context { get; private set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public DateTime FinishedAt { get; private set; }

    public BotContextAccessor() { }

    public void Initialize(ExtendedShardedInteractionContext context, Models.ServerMeta serverMeta, IConfiguration config)
    {
        Context = context;
        ServerMeta = serverMeta;

        BotName = config.GetValue<string>(Constants.Environment.BotName) ?? BotName;
        BotVersion = config.GetValue<string>(Constants.Environment.BotVersion) ?? BotVersion;
    }

    public void Finish()
    {
        FinishedAt = DateTime.UtcNow;
    }

    public void SetServerMeta(Models.ServerMeta serverMeta)
    {
        if (serverMeta is null)
        {
            throw new ArgumentNullException(nameof(serverMeta), "ServerMeta cannot be null");
        }

        ServerMeta = serverMeta;
    }

    public void SetContext(ExtendedShardedInteractionContext context)
    {
        Context = context;
    }
}