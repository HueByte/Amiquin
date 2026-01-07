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

    /// <summary>
    /// Default models to always show, even if config is empty.
    /// This is the single source of truth for available models.
    /// </summary>
    public static readonly Dictionary<string, string> DefaultModels = new()
    {
        // OpenAI - GPT-5 series
        { "gpt-5.2", "OpenAI" },
        { "gpt-5.1", "OpenAI" },
        { "gpt-5", "OpenAI" },
        { "gpt-5-mini", "OpenAI" },
        { "gpt-5-nano", "OpenAI" },
        { "gpt-5.2-chat-latest", "OpenAI" },
        { "gpt-5.1-chat-latest", "OpenAI" },
        { "gpt-5-chat-latest", "OpenAI" },
        { "gpt-5.1-codex-max", "OpenAI" },
        { "gpt-5.1-codex", "OpenAI" },
        { "gpt-5-codex", "OpenAI" },
        { "gpt-5.2-pro", "OpenAI" },
        { "gpt-5-pro", "OpenAI" },
        // OpenAI - GPT-4 series
        { "gpt-4.1", "OpenAI" },
        { "gpt-4.1-mini", "OpenAI" },
        { "gpt-4.1-nano", "OpenAI" },
        { "gpt-4o", "OpenAI" },
        { "gpt-4o-mini", "OpenAI" },
        { "gpt-4-turbo", "OpenAI" },
        { "gpt-4", "OpenAI" },
        { "gpt-3.5-turbo", "OpenAI" },
        // OpenAI - o-series reasoning models
        { "o1", "OpenAI" },
        { "o1-pro", "OpenAI" },
        { "o1-mini", "OpenAI" },
        { "o3-pro", "OpenAI" },
        { "o3", "OpenAI" },
        { "o3-mini", "OpenAI" },
        { "o3-deep-research", "OpenAI" },
        { "o4-mini", "OpenAI" },
        { "o4-mini-deep-research", "OpenAI" },
        // Gemini
        { "gemini-2.0-flash-exp", "Gemini" },
        { "gemini-1.5-pro", "Gemini" },
        { "gemini-1.5-flash", "Gemini" },
        { "gemini-1.5-flash-8b", "Gemini" },
        // Grok
        { "grok-4", "Grok" },
        { "grok-3", "Grok" },
        { "grok-3-mini", "Grok" },
        { "grok-2", "Grok" },
        { "grok-2-mini", "Grok" },
        // Anthropic
        { "claude-3-5-sonnet-latest", "Anthropic" },
        { "claude-3-5-haiku-latest", "Anthropic" },
        { "claude-3-opus-latest", "Anthropic" },
    };

    /// <summary>
    /// Model display names for UI presentation
    /// </summary>
    private static readonly Dictionary<string, string> ModelDisplayNames = new()
    {
        // OpenAI GPT-5
        { "gpt-5.2", "GPT-5.2 - Latest flagship" },
        { "gpt-5.1", "GPT-5.1 - Advanced" },
        { "gpt-5", "GPT-5 - Flagship" },
        { "gpt-5-mini", "GPT-5 Mini - Compact" },
        { "gpt-5-nano", "GPT-5 Nano - Lightweight" },
        // OpenAI GPT-4
        { "gpt-4.1", "GPT-4.1 - Latest GPT-4" },
        { "gpt-4.1-mini", "GPT-4.1 Mini - Compact" },
        { "gpt-4.1-nano", "GPT-4.1 Nano - Lightweight" },
        { "gpt-4o", "GPT-4o - Omni capable" },
        { "gpt-4o-mini", "GPT-4o Mini - Budget" },
        { "gpt-4-turbo", "GPT-4 Turbo - Fast" },
        { "gpt-4", "GPT-4 - Original" },
        { "gpt-3.5-turbo", "GPT-3.5 Turbo - Legacy" },
        // OpenAI o-series
        { "o1", "o1 - Reasoning" },
        { "o1-pro", "o1 Pro - Advanced reasoning" },
        { "o1-mini", "o1 Mini - Fast reasoning" },
        { "o3-pro", "o3 Pro - Premium reasoning" },
        { "o3", "o3 - Advanced reasoning" },
        { "o3-mini", "o3 Mini - Fast reasoning" },
        { "o3-deep-research", "o3 Deep Research" },
        { "o4-mini", "o4 Mini - Latest reasoning" },
        { "o4-mini-deep-research", "o4 Mini Deep Research" },
        // Gemini
        { "gemini-2.0-flash-exp", "Gemini 2.0 Flash - Latest" },
        { "gemini-1.5-pro", "Gemini 1.5 Pro - Capable" },
        { "gemini-1.5-flash", "Gemini 1.5 Flash - Fast" },
        { "gemini-1.5-flash-8b", "Gemini 1.5 Flash 8B - Light" },
        // Grok
        { "grok-4", "Grok-4 - Latest" },
        { "grok-3", "Grok-3 - Advanced" },
        { "grok-3-mini", "Grok-3 Mini - Fast" },
        { "grok-2", "Grok-2 - Stable" },
        { "grok-2-mini", "Grok-2 Mini - Light" },
        // Anthropic
        { "claude-3-5-sonnet-latest", "Claude 3.5 Sonnet - Balanced" },
        { "claude-3-5-haiku-latest", "Claude 3.5 Haiku - Fast" },
        { "claude-3-opus-latest", "Claude 3 Opus - Most capable" },
    };

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

    /// <inheritdoc/>
    public List<(string ModelId, string DisplayName)> GetModelsForProvider(string provider)
    {
        var normalizedProvider = provider.ToLower() switch
        {
            "openai" => "OpenAI",
            "anthropic" => "Anthropic",
            "gemini" => "Gemini",
            "grok" => "Grok",
            _ => provider
        };

        return _modelProviderMap
            .Where(kvp => kvp.Value.Equals(normalizedProvider, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => (kvp.Key, ModelDisplayNames.TryGetValue(kvp.Key, out var displayName) ? displayName : kvp.Key))
            .OrderBy(m => GetModelSortOrder(m.Item1))
            .ThenBy(m => m.Item1)
            .ToList();
    }

    private static int GetModelSortOrder(string modelId)
    {
        // Order by model generation/capability (newer/better first)
        if (modelId.StartsWith("gpt-5")) return 0;
        if (modelId.StartsWith("o4")) return 1;
        if (modelId.StartsWith("o3")) return 2;
        if (modelId.StartsWith("o1")) return 3;
        if (modelId.StartsWith("gpt-4.1")) return 4;
        if (modelId.StartsWith("gpt-4o")) return 5;
        if (modelId.StartsWith("gpt-4")) return 6;
        if (modelId.StartsWith("grok-4")) return 0;
        if (modelId.StartsWith("grok-3")) return 1;
        if (modelId.StartsWith("grok-2")) return 2;
        if (modelId.StartsWith("gemini-2")) return 0;
        if (modelId.StartsWith("gemini-1.5-pro")) return 1;
        if (modelId.StartsWith("gemini-1.5-flash")) return 2;
        if (modelId.Contains("sonnet")) return 0;
        if (modelId.Contains("haiku")) return 1;
        if (modelId.Contains("opus")) return 2;
        return 99;
    }

    private Dictionary<string, string> BuildModelProviderMap()
    {
        // Start with default models
        var map = new Dictionary<string, string>(DefaultModels);

        // Override with config-defined models (config takes precedence)
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