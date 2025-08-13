using Discord;
using Discord.Interactions;

namespace Amiquin.Bot.Commands.AutoComplete;

/// <summary>
/// Autocomplete handler for channel selection
/// </summary>
public class ChannelAutoCompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var suggestions = new List<AutocompleteResult>();

            if (context.Guild == null)
            {
                return AutocompletionResult.FromSuccess(suggestions);
            }

            var userInput = autocompleteInteraction.Data.Current.Value?.ToString()?.ToLowerInvariant() ?? "";

            var channels = await context.Guild.GetChannelsAsync();
            var textChannels = channels
                .OfType<ITextChannel>()
                .OrderBy(c => c.Position)
                .Where(c => string.IsNullOrWhiteSpace(userInput) ||
                           c.Name.ToLowerInvariant().Contains(userInput) ||
                           c.Id.ToString().Contains(userInput));

            foreach (var channel in textChannels.Take(25))
            {
                var displayName = $"#{channel.Name}";
                suggestions.Add(new AutocompleteResult(displayName, channel.Id.ToString()));
            }

            return AutocompletionResult.FromSuccess(suggestions);
        }
        catch
        {
            return AutocompletionResult.FromSuccess(new List<AutocompleteResult>());
        }
    }
}