namespace Amiquin.Core.Options.Configuration;

/// <summary>
/// Configuration options for AI chat functionality.
/// </summary>
public class ChatOptions
{
    public const string SectionName = "Chat";
    
    /// <summary>
    /// The chat provider to use (OpenAI, Gemini, Grok).
    /// </summary>
    public string Provider { get; set; } = "OpenAI";
    
    /// <summary>
    /// OpenAI API authentication token.
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;
    
    /// <summary>
    /// System message to define AI behavior and personality.
    /// </summary>
    public string SystemMessage { get; set; } = "You are a helpful AI assistant.";
    
    /// <summary>
    /// Maximum token limit for AI responses.
    /// </summary>
    public int TokenLimit { get; set; } = 2000;
    
    /// <summary>
    /// Whether chat functionality is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// AI model to use for chat completions.
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";
    
    /// <summary>
    /// Temperature for response randomness (0.0 - 2.0).
    /// </summary>
    public float Temperature { get; set; } = 0.6f;
    
    /// <summary>
    /// Whether to automatically fallback to another provider if the primary fails.
    /// </summary>
    public bool EnableFallback { get; set; } = false;
    
    /// <summary>
    /// Ordered list of fallback providers.
    /// </summary>
    public List<string> FallbackProviders { get; set; } = new() { "OpenAI", "Gemini", "Grok" };
}