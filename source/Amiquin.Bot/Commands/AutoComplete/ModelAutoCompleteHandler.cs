using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.ModelProvider;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for AI model selection
/// </summary>
public class ModelAutoCompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        try
        {
            var modelProviderService = services.GetRequiredService<IModelProviderMappingService>();
            var modelProviderMap = modelProviderService.GetAllModelProviderMappings();

            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Create suggestions with provider info
            foreach (var modelProvider in modelProviderMap)
            {
                var model = modelProvider.Key;
                var provider = modelProvider.Value;

                if (string.IsNullOrWhiteSpace(userInput) || model.ToLowerInvariant().Contains(userInput))
                {
                    suggestions.Add(new AutocompleteResult($"{model} ({provider})", model));
                }
            }

            // Limit to 25 suggestions as per Discord API limit
            return Task.FromResult(AutocompletionResult.FromSuccess(suggestions.Take(25)));
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
            return Task.FromResult(AutocompletionResult.FromSuccess(fallbackSuggestions));
        }
    }
}