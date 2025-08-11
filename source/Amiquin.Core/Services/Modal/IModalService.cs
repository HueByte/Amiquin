using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.Modal;

/// <summary>
/// Service for handling Discord modals with a registry-based system.
/// </summary>
public interface IModalService
{
    /// <summary>
    /// Handles a modal submission by routing it to the appropriate registered handler.
    /// </summary>
    /// <param name="modal">The modal submission to handle.</param>
    /// <returns>True if the submission was handled, false otherwise.</returns>
    Task<bool> HandleModalSubmissionAsync(SocketModal modal);

    /// <summary>
    /// Registers a modal handler for a specific custom ID prefix.
    /// </summary>
    /// <param name="prefix">The custom ID prefix to handle (e.g., "settings", "feedback").</param>
    /// <param name="handler">The handler function to execute for matching submissions.</param>
    void RegisterHandler(string prefix, Func<SocketModal, ModalContext, Task<bool>> handler);

    /// <summary>
    /// Creates a modal builder with structured custom ID.
    /// </summary>
    /// <param name="prefix">The prefix identifying the handler type.</param>
    /// <param name="title">The modal title.</param>
    /// <param name="parameters">Additional parameters to include in the custom ID.</param>
    /// <returns>A modal builder with the custom ID set.</returns>
    ModalBuilder CreateModal(string prefix, string title, params string[] parameters);

    /// <summary>
    /// Generates a custom ID for modal components.
    /// </summary>
    /// <param name="prefix">The prefix identifying the handler type.</param>
    /// <param name="parameters">Additional parameters to include in the custom ID.</param>
    /// <returns>A structured custom ID string.</returns>
    string GenerateCustomId(string prefix, params string[] parameters);

    /// <summary>
    /// Parses a modal custom ID and returns the context information.
    /// </summary>
    /// <param name="customId">The custom ID to parse.</param>
    /// <returns>The parsed modal context, or null if invalid.</returns>
    ModalContext? ParseCustomId(string customId);

    /// <summary>
    /// Gets the value of a modal component by its custom ID.
    /// </summary>
    /// <param name="modal">The modal submission.</param>
    /// <param name="customId">The component's custom ID.</param>
    /// <returns>The component value, or null if not found.</returns>
    string? GetComponentValue(SocketModal modal, string customId);
}