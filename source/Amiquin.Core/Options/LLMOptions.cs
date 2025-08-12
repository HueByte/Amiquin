namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for LLM (Large Language Model) providers
/// </summary>
public class LLMOptions
{
    public const string SectionName = "LLM";

    /// <summary>
    /// The default provider to use for chat completions
    /// </summary>
    public string DefaultProvider { get; set; } = "OpenAI";

    /// <summary>
    /// Whether to enable automatic fallback to other providers on failure
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Ordered list of providers to try as fallbacks
    /// </summary>
    public List<string> FallbackOrder { get; set; } = new() { "OpenAI", "Grok", "Gemini" };

    /// <summary>
    /// Global system message to use across all providers (if model-specific message is not set)
    /// </summary>
    public string GlobalSystemMessage { get; set; } = "You are a helpful AI assistant named Amiquin.";

    /// <summary>
    /// Global temperature setting for response randomness (0.0 - 2.0)
    /// </summary>
    public float GlobalTemperature { get; set; } = 0.6f;

    /// <summary>
    /// Global timeout in seconds for API requests
    /// </summary>
    public int GlobalTimeout { get; set; } = 120;

    /// <summary>
    /// Configuration for each provider
    /// </summary>
    public Dictionary<string, LLMProviderOptions> Providers { get; set; } = new();

    /// <summary>
    /// Gets the configuration for a specific provider
    /// </summary>
    public LLMProviderOptions? GetProvider(string providerName)
    {
        return Providers.TryGetValue(providerName, out var provider) ? provider : null;
    }

    /// <summary>
    /// Gets the default provider configuration
    /// </summary>
    public LLMProviderOptions? GetDefaultProvider()
    {
        return GetProvider(DefaultProvider);
    }
}

/// <summary>
/// Configuration for a specific LLM provider
/// </summary>
public class LLMProviderOptions
{
    /// <summary>
    /// Whether this provider is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// API key for authenticating with the provider
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the provider's API
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Default model to use for this provider
    /// </summary>
    public string DefaultModel { get; set; } = string.Empty;

    /// <summary>
    /// Provider-specific settings (e.g., SafetyThreshold for Gemini)
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Configuration for each model supported by this provider
    /// </summary>
    public Dictionary<string, LLMModelOptions> Models { get; set; } = new();

    /// <summary>
    /// Gets the configuration for a specific model
    /// </summary>
    public LLMModelOptions? GetModel(string modelName)
    {
        return Models.TryGetValue(modelName, out var model) ? model : null;
    }

    /// <summary>
    /// Gets the default model configuration
    /// </summary>
    public LLMModelOptions? GetDefaultModel()
    {
        return GetModel(DefaultModel);
    }

    /// <summary>
    /// Gets a provider-specific setting value
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default)
    {
        if (Settings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets a provider-specific setting value
    /// </summary>
    public void SetSetting<T>(string key, T value)
    {
        if (value != null)
        {
            Settings[key] = value;
        }
    }
}

/// <summary>
/// Configuration for a specific model within a provider
/// </summary>
public class LLMModelOptions
{
    /// <summary>
    /// Human-readable name for the model
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of tokens in the context window
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// Maximum number of tokens that can be generated in the output
    /// </summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>
    /// Model-specific system message (overrides global system message if set)
    /// </summary>
    public string? SystemMessage { get; set; }

    /// <summary>
    /// Model-specific temperature (overrides global temperature if set)
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Model-specific settings
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// Gets the effective system message (model-specific or global)
    /// </summary>
    public string GetEffectiveSystemMessage(string globalSystemMessage)
    {
        return !string.IsNullOrEmpty(SystemMessage) ? SystemMessage : globalSystemMessage;
    }

    /// <summary>
    /// Gets the effective temperature (model-specific or global)
    /// </summary>
    public float GetEffectiveTemperature(float globalTemperature)
    {
        return Temperature ?? globalTemperature;
    }

    /// <summary>
    /// Gets a model-specific setting value
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default)
    {
        if (Settings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Sets a model-specific setting value
    /// </summary>
    public void SetSetting<T>(string key, T value)
    {
        if (value != null)
        {
            Settings[key] = value;
        }
    }
}