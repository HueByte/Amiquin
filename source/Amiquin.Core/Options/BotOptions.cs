using Amiquin.Core.Attributes;

namespace Amiquin.Core.Options;

public class BotOptions : IOption
{
    public const string Bot = "Bot";

    [Anomify]
    public string Token { get; set; } = default!;

    [Anomify]
    public string OpenAIKey { get; set; } = default!;

    public int MessageFetchCount { get; set; } = default!;
    public int MaxTokens { get; set; }
    public string BotName { get; set; } = default!;
    public string Version { get; set; } = default!;
}