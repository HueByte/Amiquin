using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for admin settings configuration
/// </summary>
public class AdminSettingAutoCompleteHandler : AutocompleteHandler
{
    private static readonly Dictionary<string, string> AvailableSettings = new()
    {
        { "main-channel", "Set the primary channel for bot operations" },
        { "persona", "Configure server-wide AI persona" },
        { "provider", "Set the preferred LLM provider (OpenAI, Gemini, Grok)" },
        { "welcome-channel", "Set the welcome messages channel" },
        { "log-channel", "Set the logging channel" },
        { "voice-channel", "Set the default voice channel" },
        { "announcement-channel", "Set the announcement channel" }
    };

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";
            var suggestions = new List<AutocompleteResult>();

            foreach (var setting in AvailableSettings)
            {
                if (string.IsNullOrWhiteSpace(userInput) || 
                    setting.Key.Contains(userInput) || 
                    setting.Value.ToLowerInvariant().Contains(userInput))
                {
                    suggestions.Add(new AutocompleteResult($"{setting.Key} - {setting.Value}", setting.Key));
                }
            }

            return Task.FromResult(AutocompletionResult.FromSuccess(suggestions.Take(25)));
        }
        catch
        {
            var fallbackSuggestions = new List<AutocompleteResult>
            {
                new("main-channel - Set the primary channel", "main-channel"),
                new("persona - Configure server AI persona", "persona"),
                new("provider - Set the preferred LLM provider", "provider")
            };
            return Task.FromResult(AutocompletionResult.FromSuccess(fallbackSuggestions));
        }
    }
}