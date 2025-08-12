using Amiquin.Core.Options;
using Microsoft.Extensions.Options;

namespace Amiquin.Core.Services.ModelProvider;

/// <summary>
/// Service implementation for mapping AI models to their respective providers
/// </summary>
public class ModelProviderMappingService : IModelProviderMappingService
{
    private readonly LLMOptions _llmOptions;
    private readonly Dictionary<string, string> _modelProviderMap;

    public ModelProviderMappingService(IOptions<LLMOptions> llmOptions)
    {
        _llmOptions = llmOptions.Value;
        _modelProviderMap = BuildModelProviderMap();
    }

    /// <inheritdoc/>
    public string? GetProviderForModel(string model)
    {
        return _modelProviderMap.TryGetValue(model, out var provider) ? provider : null;
    }

    /// <inheritdoc/>
    public Dictionary<string, string> GetAllModelProviderMappings()
    {
        return new Dictionary<string, string>(_modelProviderMap);
    }

    /// <inheritdoc/>
    public bool IsModelAvailable(string model)
    {
        return _modelProviderMap.ContainsKey(model);
    }

    /// <inheritdoc/>
    public List<string> GetAvailableModels()
    {
        return _modelProviderMap.Keys.ToList();
    }

    private Dictionary<string, string> BuildModelProviderMap()
    {
        var map = new Dictionary<string, string>();

        foreach (var provider in _llmOptions.Providers)
        {
            var providerName = provider.Key;
            var providerConfig = provider.Value;

            // Only include enabled providers
            if (!providerConfig.Enabled)
                continue;

            // Add all models for this provider
            if (providerConfig.Models != null)
            {
                foreach (var model in providerConfig.Models)
                {
                    var modelName = model.Key;
                    map[modelName] = providerName;
                }
            }
        }

        return map;
    }
}