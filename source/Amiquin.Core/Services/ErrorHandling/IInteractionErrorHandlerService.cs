using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.ErrorHandling;

/// <summary>
/// Service for handling errors in Discord interactions with standardized responses.
/// </summary>
public interface IInteractionErrorHandlerService
{
    /// <summary>
    /// Handles an exception that occurred during interaction processing.
    /// </summary>
    /// <param name="interaction">The interaction that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="contextInfo">Additional context information for logging.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleInteractionErrorAsync(SocketInteraction interaction, Exception exception, string? contextInfo = null);

    /// <summary>
    /// Creates standardized error components for interaction responses.
    /// </summary>
    /// <param name="title">The error title.</param>
    /// <param name="description">The error description.</param>
    /// <returns>MessageComponent with error information.</returns>
    MessageComponent CreateErrorComponents(string title, string description);

    /// <summary>
    /// Responds to an interaction with an error message using the appropriate method.
    /// </summary>
    /// <param name="interaction">The interaction to respond to.</param>
    /// <param name="message">The error message to send.</param>
    /// <param name="isEphemeral">Whether the response should be ephemeral (default: true).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RespondWithErrorAsync(SocketInteraction interaction, string message, bool isEphemeral = true);
}