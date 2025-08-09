namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Options for configuring chat completions across different providers
/// </summary>
public class ChatCompletionOptions
{
    /// <summary>
    /// Maximum number of tokens to generate in the completion
    /// </summary>
    public int MaxTokens { get; set; } = 1200;
    
    /// <summary>
    /// Temperature for randomness (0.0 = deterministic, 2.0 = very random)
    /// </summary>
    public float Temperature { get; set; } = 0.6f;
    
    /// <summary>
    /// Top-p nucleus sampling parameter
    /// </summary>
    public float? TopP { get; set; }
    
    /// <summary>
    /// Number of completions to generate
    /// </summary>
    public int N { get; set; } = 1;
    
    /// <summary>
    /// Stop sequences to end generation
    /// </summary>
    public List<string>? StopSequences { get; set; }
    
    /// <summary>
    /// Presence penalty (-2.0 to 2.0)
    /// </summary>
    public float? PresencePenalty { get; set; }
    
    /// <summary>
    /// Frequency penalty (-2.0 to 2.0)
    /// </summary>
    public float? FrequencyPenalty { get; set; }
    
    /// <summary>
    /// User identifier for tracking
    /// </summary>
    public string? User { get; set; }
    
    /// <summary>
    /// The specific model to use (if provider supports multiple models)
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Additional provider-specific options
    /// </summary>
    public Dictionary<string, object>? AdditionalOptions { get; set; }
}