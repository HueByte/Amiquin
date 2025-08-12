using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.Pagination;

/// <summary>
/// Service interface for handling paginated embeds with Discord components
/// </summary>
public interface IPaginationService
{
    /// <summary>
    /// Creates a paginated message with navigation buttons
    /// </summary>
    /// <param name="embeds">The embeds to paginate through</param>
    /// <param name="userId">ID of the user who can interact with the pagination</param>
    /// <param name="timeout">How long the pagination should remain active (default: 5 minutes)</param>
    /// <returns>A tuple containing the embed and component for the first page</returns>
    Task<(Embed Embed, MessageComponent Component)> CreatePaginatedMessageAsync(
        IReadOnlyList<Embed> embeds,
        ulong userId,
        TimeSpan? timeout = null);

    /// <summary>
    /// Handles component interactions for pagination
    /// </summary>
    /// <param name="component">The component interaction</param>
    /// <returns>True if the interaction was handled by this service</returns>
    Task<bool> HandleInteractionAsync(SocketMessageComponent component);

    /// <summary>
    /// Creates a pagination session ID for tracking interactions
    /// </summary>
    /// <param name="userId">User ID for the session</param>
    /// <returns>A unique session ID</returns>
    string CreateSessionId(ulong userId);
}