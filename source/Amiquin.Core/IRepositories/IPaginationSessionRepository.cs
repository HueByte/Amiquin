using Amiquin.Core.Models;

namespace Amiquin.Core.IRepositories;

/// <summary>
/// Repository interface for managing pagination sessions
/// </summary>
public interface IPaginationSessionRepository
{
    /// <summary>
    /// Creates a new pagination session in the database
    /// </summary>
    /// <param name="session">The pagination session to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session</returns>
    Task<PaginationSession> CreateAsync(PaginationSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a pagination session by ID
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pagination session or null if not found</returns>
    Task<PaginationSession?> GetByIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing pagination session
    /// </summary>
    /// <param name="session">The session to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated session</returns>
    Task<PaginationSession> UpdateAsync(PaginationSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a pagination session
    /// </summary>
    /// <param name="sessionId">The session ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active pagination sessions for a specific user
    /// </summary>
    /// <param name="userId">The Discord user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active pagination sessions</returns>
    Task<List<PaginationSession>> GetActiveByUserAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all expired pagination sessions that need cleanup
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of expired pagination sessions</returns>
    Task<List<PaginationSession>> GetExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all expired pagination sessions (cleanup operation)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a pagination session by message ID
    /// </summary>
    /// <param name="messageId">The Discord message ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pagination session or null if not found</returns>
    Task<PaginationSession?> GetByMessageIdAsync(ulong messageId, CancellationToken cancellationToken = default);
}