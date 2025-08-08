namespace Amiquin.Core.Services.Chat;

/// <summary>
/// Defines the contract for message management services that handle persona messages and basic message operations.
/// </summary>
public interface IMessageManager
{
    /// <summary>
    /// Gets the core persona message for the AI system.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the persona message as a string.</returns>
    Task<string> GetPersonaCoreMessageAsync();
}