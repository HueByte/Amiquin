using Amiquin.Core.Services.ChatSession;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for AI provider selection
/// </summary>
public class ProviderAutoCompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        try
        {
            var chatSessionService = services.GetRequiredService<IChatSessionService>();
            var availableModels = await chatSessionService.GetAvailableModelsAsync();

            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Get all available providers
            foreach (var provider in availableModels.Keys)
            {
                if (string.IsNullOrWhiteSpace(userInput) || provider.ToLowerInvariant().Contains(userInput))
                {
                    var modelCount = availableModels[provider].Count;
                    suggestions.Add(new AutocompleteResult($"{provider} ({modelCount} models)", provider));
                }
            }

            // Limit to 25 suggestions as per Discord API limit
            return AutocompletionResult.FromSuccess(suggestions.Take(25));
        }
        catch
        {
            // Fallback suggestions if service fails
            var fallbackSuggestions = new List<AutocompleteResult>
            {
                new("OpenAI", "OpenAI"),
                new("Grok", "Grok"),
                new("Gemini", "Gemini")
            };
            return AutocompletionResult.FromSuccess(fallbackSuggestions);
        }
    }
}