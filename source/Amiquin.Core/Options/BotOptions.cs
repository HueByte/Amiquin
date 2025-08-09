using System.Reflection;

namespace Amiquin.Core.Options;

public class BotOptions : IOption
{
    public const string Bot = "Bot";

    public string Name { get; set; } = Constants.DefaultValues.BotName;
    public bool PrintLogo { get; set; } = false;
    public int MessageFetchCount { get; set; } = 40;
    public int MaxTokens { get; set; } = 20000;
    
    /// <summary>
    /// Gets the version from the assembly, not from configuration.
    /// </summary>
    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? Constants.DefaultValues.UnknownValue;
}