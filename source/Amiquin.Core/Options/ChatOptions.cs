namespace Amiquin.Core.Options;

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

    /// <summary>
    /// ReAct (Reason-Act-Think) loop configuration for enhanced conversation handling.
    /// </summary>
    public ReActOptions ReAct { get; set; } = new();
}

/// <summary>
/// Configuration options for the ReAct (Reason-Act-Think) loop.
/// Enables lightweight reasoning to improve response quality.
/// </summary>
public class ReActOptions
{
    /// <summary>
    /// Whether ReAct reasoning is enabled.
    /// When enabled, the bot performs a lightweight reasoning step before responding.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Maximum number of reasoning iterations (3-5 recommended for impressive yet lightweight reasoning).
    /// Each iteration allows the bot to think, act, and observe before responding.
    /// </summary>
    public int MaxIterations { get; set; } = 4;

    /// <summary>
    /// Whether to include the reasoning trace in logs (useful for debugging).
    /// </summary>
    public bool LogReasoningTrace { get; set; } = true;

    /// <summary>
    /// Minimum message length to trigger ReAct reasoning.
    /// Short messages (greetings, etc.) skip reasoning for faster responses.
    /// </summary>
    public int MinMessageLengthForReasoning { get; set; } = 15;

    /// <summary>
    /// Whether to use memories in the reasoning process.
    /// When enabled, relevant memories inform the reasoning step.
    /// </summary>
    public bool UseMemoriesInReasoning { get; set; } = true;

    /// <summary>
    /// Token limit for the reasoning step (kept low to minimize latency).
    /// </summary>
    public int ReasoningTokenLimit { get; set; } = 350;

    /// <summary>
    /// Whether to allow the bot to reflect on its reasoning quality.
    /// Adds a self-evaluation step that can improve response quality.
    /// </summary>
    public bool EnableSelfReflection { get; set; } = true;

    /// <summary>
    /// Confidence threshold (0.0-1.0) below which the bot will try additional reasoning.
    /// Lower values mean more iterations, higher values mean faster responses.
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.7f;
}