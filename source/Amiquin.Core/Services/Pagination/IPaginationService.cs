using Discord;
using Discord.WebSocket;

namespace Amiquin.Core.Services.Pagination;

/// <summary>
/// Service interface for handling paginated content with Discord ComponentsV2
/// </summary>
public interface IPaginationService
{
    /// <summary>
    /// Creates a paginated message with navigation buttons using ComponentsV2
    /// </summary>
    /// <param name="pages">The page contents to paginate through</param>
    /// <param name="userId">ID of the user who can interact with the pagination</param>
    /// <param name="timeout">How long the pagination should remain active (default: 5 minutes)</param>
    /// <returns>The MessageComponent for the first page</returns>
    Task<MessageComponent> CreatePaginatedMessageAsync(
        IReadOnlyList<PaginationPage> pages,
        ulong userId,
        TimeSpan? timeout = null);
    
    /// <summary>
    /// Creates a paginated message from embeds (for backward compatibility)
    /// </summary>
    Task<MessageComponent> CreatePaginatedMessageFromEmbedsAsync(
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