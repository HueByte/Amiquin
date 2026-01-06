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
    // Default models to always show, even if config is empty
    private static readonly Dictionary<string, string> DefaultModels = new()
    {
        // OpenAI - GPT-5 series
        { "gpt-5.2", "OpenAI" },
        { "gpt-5.1", "OpenAI" },
        { "gpt-5", "OpenAI" },
        { "gpt-5-mini", "OpenAI" },
        { "gpt-5-nano", "OpenAI" },
        { "gpt-5.2-chat-latest", "OpenAI" },
        { "gpt-5.1-chat-latest", "OpenAI" },
        { "gpt-5-chat-latest", "OpenAI" },
        { "gpt-5.1-codex-max", "OpenAI" },
        { "gpt-5.1-codex", "OpenAI" },
        { "gpt-5-codex", "OpenAI" },
        { "gpt-5.2-pro", "OpenAI" },
        { "gpt-5-pro", "OpenAI" },
        // OpenAI - GPT-4 series
        { "gpt-4.1", "OpenAI" },
        { "gpt-4.1-mini", "OpenAI" },
        { "gpt-4.1-nano", "OpenAI" },
        { "gpt-4o", "OpenAI" },
        { "gpt-4o-mini", "OpenAI" },
        { "gpt-4-turbo", "OpenAI" },
        { "gpt-4", "OpenAI" },
        { "gpt-3.5-turbo", "OpenAI" },
        // OpenAI - o-series reasoning models
        { "o1", "OpenAI" },
        { "o1-pro", "OpenAI" },
        { "o1-mini", "OpenAI" },
        { "o3-pro", "OpenAI" },
        { "o3", "OpenAI" },
        { "o3-mini", "OpenAI" },
        { "o3-deep-research", "OpenAI" },
        { "o4-mini", "OpenAI" },
        { "o4-mini-deep-research", "OpenAI" },
        // Gemini
        { "gemini-2.0-flash-exp", "Gemini" },
        { "gemini-1.5-pro", "Gemini" },
        { "gemini-1.5-flash", "Gemini" },
        { "gemini-1.5-flash-8b", "Gemini" },
        // Grok
        { "grok-4", "Grok" },
        { "grok-3", "Grok" },
        { "grok-3-mini", "Grok" },
        { "grok-2", "Grok" },
        { "grok-2-mini", "Grok" },
        // Anthropic
        { "claude-3-5-sonnet-latest", "Anthropic" },
        { "claude-3-5-haiku-latest", "Anthropic" },
        { "claude-3-opus-latest", "Anthropic" },
    };

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        try
        {
            var suggestions = new List<AutocompleteResult>();
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            // Try to get models from configuration
            var modelProviderService = services.GetService<IModelProviderMappingService>();
            var modelProviderMap = modelProviderService?.GetAllModelProviderMappings() ?? new Dictionary<string, string>();

            // Merge with default models (config takes precedence)
            var allModels = new Dictionary<string, string>(DefaultModels);
            foreach (var kvp in modelProviderMap)
            {
                allModels[kvp.Key] = kvp.Value;
            }

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
            var fallbackSuggestions = DefaultModels.Take(25)
                .Select(m => new AutocompleteResult($"{m.Key} ({m.Value})", m.Key))
                .ToList();
            return Task.FromResult(AutocompletionResult.FromSuccess(fallbackSuggestions));
        }
    }
}
