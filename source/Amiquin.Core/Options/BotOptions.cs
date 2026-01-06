using System.Reflection;

namespace Amiquin.Core.Options;

public class BotOptions : IOption
{
    public const string Bot = "Bot";

    public string Name { get; set; } = Constants.DefaultValues.BotName;
    public bool PrintLogo { get; set; } = false;
    public int MessageFetchCount { get; set; } = 40;

    /// <summary>
    /// Maximum tokens per conversation before history optimization kicks in.
    /// This is separate from model context limits - it controls when conversation
    /// compaction triggers. Default: 40000 tokens.
    /// </summary>
    public int ConversationTokenLimit { get; set; } = 40000;

    /// <summary>
    /// Threshold (0.0-1.0) of ConversationTokenLimit at which history optimization is triggered.
    /// Default: 0.8 (80% of limit triggers compaction).
    /// </summary>
    public float HistoryOptimizationThreshold { get; set; } = 0.8f;

    /// <summary>
    /// List of server IDs that are allowed to use the bot.
    /// If empty, all servers are allowed.
    /// </summary>
    public List<ulong> ServerWhitelist { get; set; } = [];

    /// <summary>
    /// Gets the version from the assembly, not from configuration.
    /// </summary>
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

    /// <summary>
    /// Checks if a server is allowed to use the bot.
    /// Returns true if whitelist is empty (all allowed) or if the server ID is in the whitelist.
    /// </summary>
    public bool IsServerAllowed(ulong serverId)
    {
        return ServerWhitelist.Count == 0 || ServerWhitelist.Contains(serverId);
    }
}