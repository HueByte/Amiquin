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
            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Try to get models from service (includes defaults + config)
            var modelProviderService = services.GetService<IModelProviderMappingService>();
            var allModels = modelProviderService?.GetAllModelProviderMappings()
                ?? ModelProviderMappingService.DefaultModels;

            // Create suggestions with provider info
            foreach (var modelProvider in allModels)
            {
                var model = modelProvider.Key;
                var provider = modelProvider.Value;

                if (string.IsNullOrWhiteSpace(userInput) || model.ToLowerInvariant().Contains(userInput) || provider.ToLowerInvariant().Contains(userInput))
                {
                    suggestions.Add(new AutocompleteResult($"{model} ({provider})", model));
                }
            }

            // Sort by provider then model name for better UX
            var sortedSuggestions = suggestions
                .OrderBy(s => s.Name.Contains("OpenAI") ? 0 : s.Name.Contains("Gemini") ? 1 : s.Name.Contains("Grok") ? 2 : s.Name.Contains("Anthropic") ? 3 : 4)
                .ThenBy(s => s.Name)
                .Take(25)
                .ToList();

            return Task.FromResult(AutocompletionResult.FromSuccess(sortedSuggestions));
        }
        catch
        {
            // Fallback suggestions if something fails
            var fallbackSuggestions = ModelProviderMappingService.DefaultModels.Take(25)
                .Select(m => new AutocompleteResult($"{m.Key} ({m.Value})", m.Key))
                .ToList();
            return Task.FromResult(AutocompletionResult.FromSuccess(fallbackSuggestions));
        }
    }
}
