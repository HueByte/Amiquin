using Amiquin.Core;
using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for toggle selection
/// </summary>
public class ToggleAutoCompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Add regular toggles
            foreach (var toggle in Constants.ToggleNames.Toggles)
            {
                if (string.IsNullOrWhiteSpace(userInput) || toggle.ToLowerInvariant().Contains(userInput))
                {
                    var displayName = GetToggleDisplayName(toggle);
                    suggestions.Add(new AutocompleteResult(displayName, toggle));
                }
            }

            // Add system exclusive toggles for admins
            foreach (var toggle in Constants.ToggleNames.SystemExclusiveTogglesList)
            {
                if (string.IsNullOrWhiteSpace(userInput) || toggle.ToLowerInvariant().Contains(userInput))
                {
                    var displayName = $"[System] {GetToggleDisplayName(toggle)}";
                    suggestions.Add(new AutocompleteResult(displayName, toggle));
                }
            }

            return Task.FromResult(AutocompletionResult.FromSuccess(suggestions.Take(25)));
        }
        catch
        {
            // Fallback suggestions if service fails
            var fallbackSuggestions = new List<AutocompleteResult>
            {
                new("EnableChat - Enable AI chat", Constants.ToggleNames.EnableChat),
                new("EnableTTS - Enable text-to-speech", Constants.ToggleNames.EnableTTS),
                new("EnableJoinMessage - Enable join messages", Constants.ToggleNames.EnableJoinMessage)
            };
            return Task.FromResult(AutocompletionResult.FromSuccess(fallbackSuggestions));
        }
    }

    private string GetToggleDisplayName(string toggleName)
    {
        return toggleName switch
        {
            Constants.ToggleNames.EnableChat => "EnableChat - Enable AI chat functionality",
            Constants.ToggleNames.EnableTTS => "EnableTTS - Enable text-to-speech",
            Constants.ToggleNames.EnableJoinMessage => "EnableJoinMessage - Enable welcome messages",
            Constants.ToggleNames.EnableLiveJob => "EnableLiveJob - Enable background jobs",
            Constants.ToggleNames.EnableAIWelcome => "EnableAIWelcome - Enable AI-powered welcome messages",
            Constants.ToggleNames.SystemExclusiveToggles.EnableNews => "EnableNews - Enable news updates",
            _ => toggleName
        };
    }
}