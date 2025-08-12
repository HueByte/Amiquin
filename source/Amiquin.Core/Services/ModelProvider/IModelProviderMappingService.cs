namespace Amiquin.Core.Services.ModelProvider;

/// <summary>
/// Service for mapping AI models to their respective providers
/// </summary>
public interface IModelProviderMappingService
{
    /// <summary>
    /// Gets the provider name for a given model
    /// </summary>
    /// <param name="model">The model name</param>
    /// <returns>The provider name, or null if not found</returns>
    string? GetProviderForModel(string model);

    /// <summary>
    /// Gets all available models with their providers
    /// </summary>
    /// <returns>Dictionary with model name as key and provider as value</returns>
    Dictionary<string, string> GetAllModelProviderMappings();

    /// <summary>
    /// Checks if a model is valid and available
    /// </summary>
    /// <param name="model">The model name</param>
    /// <returns>True if the model is available</returns>
    bool IsModelAvailable(string model);

    /// <summary>
    /// Gets all available models for autocomplete
    /// </summary>
    /// <returns>List of model names</returns>
    List<string> GetAvailableModels();
}