using Amiquin.Core.Attributes;

namespace Amiquin.Core.Options;

public class BotOptions : IOption
{
    public const string BOT = "Bot";

    [Anomify]
    public string Token { get; set; } = default!;

    [Anomify]
    public string OpenAIKey { get; set; } = default!;
}