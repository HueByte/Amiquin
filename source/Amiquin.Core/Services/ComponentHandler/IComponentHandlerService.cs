using Discord.WebSocket;

namespace Amiquin.Core.Services.ComponentHandler;

/// <summary>
/// Service for handling Discord component interactions with a registry-based system.
/// </summary>
public interface IComponentHandlerService
{
    /// <summary>
    /// Handles a component interaction by routing it to the appropriate registered handler.
    /// </summary>
    /// <param name="component">The component interaction to handle.</param>
    /// <returns>True if the interaction was handled, false otherwise.</returns>
    Task<bool> HandleInteractionAsync(SocketMessageComponent component);

    /// <summary>
    /// Registers a component handler for a specific custom ID prefix.
    /// </summary>
    /// <param name="prefix">The custom ID prefix to handle (e.g., "pagination", "settings").</param>
    /// <param name="handler">The handler function to execute for matching interactions.</param>
    void RegisterHandler(string prefix, Func<SocketMessageComponent, ComponentContext, Task<bool>> handler);

    /// <summary>
    /// Generates a custom ID with the specified prefix and parameters.
    /// </summary>
    /// <param name="prefix">The prefix identifying the handler type.</param>
    /// <param name="parameters">Additional parameters to include in the custom ID.</param>
    /// <returns>A structured custom ID string.</returns>
    string GenerateCustomId(string prefix, params string[] parameters);

    /// <summary>
    /// Parses a custom ID and returns the context information.
    /// </summary>
    /// <param name="customId">The custom ID to parse.</param>
    /// <returns>The parsed component context, or null if invalid.</returns>
    ComponentContext? ParseCustomId(string customId);

    /// <summary>
    /// Determines if the given custom ID will trigger a modal response.
    /// This helps the event handler decide whether to defer the interaction.
    /// </summary>
    /// <param name="customId">The custom ID to check.</param>
    /// <returns>True if the interaction will respond with a modal, false otherwise.</returns>
    bool WillTriggerModal(string customId);

    /// <summary>
    /// Registers a prefix as one that will trigger modal responses.
    /// This prevents the event handler from deferring interactions with this prefix.
    /// </summary>
    /// <param name="prefix">The prefix that triggers modal responses.</param>
    void RegisterModalTrigger(string prefix);
}