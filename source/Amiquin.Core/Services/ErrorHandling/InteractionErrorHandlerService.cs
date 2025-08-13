using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.ErrorHandling;

/// <summary>
/// Implementation of the interaction error handler service.
/// Provides centralized error handling for all Discord interactions.
/// </summary>
public class InteractionErrorHandlerService : IInteractionErrorHandlerService
{
    private const string DefaultErrorMessage = "An error occurred while processing your interaction. Please try again later.";

    private readonly ILogger<InteractionErrorHandlerService> _logger;

    public InteractionErrorHandlerService(ILogger<InteractionErrorHandlerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task HandleInteractionErrorAsync(SocketInteraction interaction, Exception exception, string? contextInfo = null)
    {
        var context = !string.IsNullOrEmpty(contextInfo) ? $" - {contextInfo}" : string.Empty;
        _logger.LogError(exception, "Error handling interaction {InteractionType} with ID {InteractionId}{Context}",
            interaction.Type, interaction.Id, context);

        try
        {
            // Special handling for modal-related errors
            if (exception is InvalidOperationException invalidOp &&
                invalidOp.Message.Contains("Cannot respond twice to the same interaction"))
            {
                _logger.LogDebug("Interaction {InteractionId} appears to be a modal command that was incorrectly deferred - this is expected behavior for modal commands.",
                    interaction.Id);
                return; // Don't try to respond, the modal should have been shown successfully
            }

            var errorMessage = exception switch
            {
                TimeoutException => "The interaction timed out. Please try again.",
                UnauthorizedAccessException => "You don't have permission to perform this action.",
                InvalidOperationException => "The interaction cannot be executed in this context.",
                ArgumentException => "Invalid arguments provided to the interaction.",
                _ => DefaultErrorMessage
            };

            await RespondWithErrorAsync(interaction, errorMessage);
        }
        catch (Exception responseEx)
        {
            _logger.LogError(responseEx, "Failed to send error response for interaction {InteractionId}", interaction.Id);
        }
    }

    /// <inheritdoc/>
    public MessageComponent CreateErrorComponents(string title, string description)
    {
        return new ComponentBuilderV2()
            .WithContainer(container =>
            {
                container.WithTextDisplay($"# {title}");
                container.WithTextDisplay(description ?? "An unexpected error occurred.");
                container.WithTextDisplay($"*Error occurred at <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:F>*");
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task RespondWithErrorAsync(SocketInteraction interaction, string message, bool isEphemeral = true)
    {
        try
        {
            // Autocomplete interactions don't support regular response methods
            if (interaction is SocketAutocompleteInteraction autocomplete)
            {
                // For autocomplete interactions, we respond with empty results to indicate failure
                await autocomplete.RespondAsync(Array.Empty<AutocompleteResult>());
                return;
            }

            if (interaction.HasResponded)
            {
                // Use Components V2 for error responses when modifying
                var errorComponents = CreateErrorComponents("❌ Error", message);
                await interaction.ModifyOriginalResponseAsync(msg =>
                {
                    msg.Components = errorComponents;
                    msg.Flags = MessageFlags.ComponentsV2;
                    msg.Content = null;
                    msg.Embed = null;
                });
            }
            else
            {
                await interaction.RespondAsync(message, ephemeral: isEphemeral);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send error response to user for interaction {InteractionId}", interaction.Id);
        }
    }
}