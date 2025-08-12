namespace Amiquin.Core.Services.Chat.Providers;

/// <summary>
/// Represents a response from an AI chat provider
/// </summary>
public class ChatCompletionResponse
{
    /// <summary>
    /// The generated text content from the AI
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The role of the response (typically "assistant")
    /// </summary>
    public string Role { get; set; } = "assistant";

    /// <summary>
    /// Number of tokens used in the prompt
    /// </summary>
    public int? PromptTokens { get; set; }

    /// <summary>
    /// Number of tokens in the completion
    /// </summary>
    public int? CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens used (prompt + completion)
    /// </summary>
    public int? TotalTokens { get; set; }

    /// <summary>
    /// The model that was used for the completion
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Any additional metadata from the provider
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Timestamp when the response was generated
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}