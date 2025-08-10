using Amiquin.Core.Services.ChatSession;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for AI model selection
/// </summary>
public class ModelAutoCompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        try
        {
            var chatSessionService = services.GetRequiredService<IChatSessionService>();
            var availableModels = await chatSessionService.GetAvailableModelsAsync();

            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Flatten all models from all providers
            foreach (var provider in availableModels)
            {
                foreach (var model in provider.Value)
                {
                    if (string.IsNullOrWhiteSpace(userInput) || model.ToLowerInvariant().Contains(userInput))
                    {
                        suggestions.Add(new AutocompleteResult($"{model} ({provider.Key})", model));
                    }
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
                new("gpt-4o", "gpt-4o"),
                new("gpt-4o-mini", "gpt-4o-mini"),
                new("gpt-4-turbo", "gpt-4-turbo")
            };
            return AutocompletionResult.FromSuccess(fallbackSuggestions);
        }
    }
}
