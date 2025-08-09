using Amiquin.Core.Models;
using Amiquin.Core.Options;
using Microsoft.Extensions.Logging;

namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Base class for LLM providers that use the new configuration system
/// </summary>
public abstract class LLMProviderBase : IChatProvider
{
    protected readonly ILogger _logger;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly LLMProviderOptions _config;
    protected readonly LLMOptions _globalConfig;
    
    protected LLMProviderBase(
        ILogger logger,
        IHttpClientFactory httpClientFactory,
        LLMProviderOptions config,
        LLMOptions globalConfig)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _globalConfig = globalConfig;
    }
    
    public abstract string ProviderName { get; }
    
    public virtual int MaxContextTokens => _config.GetDefaultModel()?.MaxTokens ?? 4096;
    
    public abstract Task<ChatCompletionResponse> ChatAsync(
        IEnumerable<SessionMessage> messages, 
        ChatCompletionOptions options);
    
    public abstract Task<bool> IsAvailableAsync();
    
    /// <summary>
    /// Gets the effective system message for a model
    /// </summary>
    protected string GetEffectiveSystemMessage(string? modelName)
    {
        var model = !string.IsNullOrEmpty(modelName) ? _config.GetModel(modelName) : _config.GetDefaultModel();
        return model?.GetEffectiveSystemMessage(_globalConfig.GlobalSystemMessage) ?? _globalConfig.GlobalSystemMessage;
    }
    
    /// <summary>
    /// Gets the effective temperature for a model
    /// </summary>
    protected float GetEffectiveTemperature(string? modelName)
    {
        var model = !string.IsNullOrEmpty(modelName) ? _config.GetModel(modelName) : _config.GetDefaultModel();
        return model?.GetEffectiveTemperature(_globalConfig.GlobalTemperature) ?? _globalConfig.GlobalTemperature;
    }
    
    /// <summary>
    /// Gets the maximum output tokens for a model
    /// </summary>
    protected int GetMaxOutputTokens(string? modelName, int defaultValue = 2048)
    {
        var model = !string.IsNullOrEmpty(modelName) ? _config.GetModel(modelName) : _config.GetDefaultModel();
        return model?.MaxOutputTokens ?? defaultValue;
    }
    
    /// <summary>
    /// Gets the maximum context tokens for a model
    /// </summary>
    protected int GetMaxContextTokens(string? modelName, int defaultValue = 4096)
    {
        var model = !string.IsNullOrEmpty(modelName) ? _config.GetModel(modelName) : _config.GetDefaultModel();
        return model?.MaxTokens ?? defaultValue;
    }
    
    /// <summary>
    /// Creates an HTTP client for this provider
    /// </summary>
    protected HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient($"LLM_{ProviderName}");
        
        // Configure base address if not already set
        if (client.BaseAddress == null && !string.IsNullOrEmpty(_config.BaseUrl))
        {
            client.BaseAddress = new Uri(_config.BaseUrl);
        }
        
        // Set timeout
        client.Timeout = TimeSpan.FromSeconds(_globalConfig.GlobalTimeout);
        
        return client;
    }
    
    /// <summary>
    /// Validates that the provider is properly configured
    /// </summary>
    protected virtual bool ValidateConfiguration()
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("{Provider} is disabled in configuration", ProviderName);
            return false;
        }
        
        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogWarning("{Provider} API key is not configured", ProviderName);
            return false;
        }
        
        if (string.IsNullOrEmpty(_config.BaseUrl))
        {
            _logger.LogWarning("{Provider} base URL is not configured", ProviderName);
            return false;
        }
        
        return true;
    }
}