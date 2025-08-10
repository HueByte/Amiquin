using ChatSessionModel = Amiquin.Core.Models.ChatSession;

namespace Amiquin.Core.Services.ChatSession;

/// <summary>
/// Service interface for managing chat sessions
/// </summary>
public interface IChatSessionService
{
    /// <summary>
    /// Gets the current active session for server scope
    /// </summary>
    /// <param name="serverId">Discord Server ID</param>
    /// <returns>Active chat session if found</returns>
    Task<ChatSessionModel?> GetActiveServerSessionAsync(ulong serverId);

    /// <summary>
    /// Gets or creates an active session for server scope
    /// </summary>
    /// <param name="serverId">Discord Server ID</param>
    /// <param name="defaultModel">Default model to use for new sessions</param>
    /// <param name="defaultProvider">Default provider to use for new sessions</param>
    /// <returns>Active chat session</returns>
    Task<ChatSessionModel> GetOrCreateServerSessionAsync(ulong serverId, string defaultModel = "gpt-4o-mini", string defaultProvider = "OpenAI");

    /// <summary>
    /// Updates the model for server-scoped sessions
    /// </summary>
    /// <param name="serverId">Discord Server ID</param>
    /// <param name="model">New model name</param>
    /// <param name="provider">New provider name</param>
    /// <returns>Number of sessions updated</returns>
    Task<int> UpdateServerSessionModelAsync(ulong serverId, string model, string provider);

    /// <summary>
    /// Gets a list of available models from configuration
    /// </summary>
    /// <returns>List of available model names with their providers</returns>
    Task<Dictionary<string, List<string>>> GetAvailableModelsAsync();

    /// <summary>
    /// Validates if a model and provider combination is available
    /// </summary>
    /// <param name="model">Model name</param>
    /// <param name="provider">Provider name</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateModelProviderAsync(string model, string provider);
}
