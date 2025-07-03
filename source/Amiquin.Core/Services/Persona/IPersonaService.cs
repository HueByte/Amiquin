namespace Amiquin.Core.Services.Persona;

/// <summary>
/// Service interface for managing server persona operations.
/// Handles persona creation, updates, and retrieval for Discord servers.
/// </summary>
public interface IPersonaService
{
    /// <summary>
    /// Adds a summary update to the server's persona.
    /// </summary>
    /// <param name="serverId">The Discord server ID to update the persona for.</param>
    /// <param name="updateMessage">The summary message to add to the persona.</param>
    Task AddSummaryAsync(ulong serverId, string updateMessage);

    /// <summary>
    /// Retrieves the current persona for a server.
    /// </summary>
    /// <param name="serverId">The Discord server ID to retrieve the persona for.</param>
    /// <returns>The server's persona string.</returns>
    Task<string> GetPersonaAsync(ulong serverId);
}
