namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Factory for creating and managing chat providers
/// </summary>
public interface IChatProviderFactory
{
    /// <summary>
    /// Gets a chat provider by name
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "OpenAI", "Gemini", "Grok")</param>
    /// <returns>The requested chat provider</returns>
    IChatProvider GetProvider(string providerName);

    /// <summary>
    /// Gets the default chat provider based on configuration
    /// </summary>
    IChatProvider GetDefaultProvider();

    /// <summary>
    /// Gets all available providers
    /// </summary>
    IEnumerable<string> GetAvailableProviders();

    /// <summary>
    /// Checks if a provider is available
    /// </summary>
    bool IsProviderAvailable(string providerName);
}