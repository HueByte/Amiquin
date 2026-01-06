namespace Amiquin.Core.Configuration;

/// <summary>
/// Configuration options for Amiquin's initiative and proactive engagement behavior.
/// Controls when and how the bot initiates conversations and enters sleep modes.
/// </summary>
public class InitiativeOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string Section = "Initiative";

    /// <summary>
    /// Whether initiative-based engagement is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Deep sleep configuration - when the bot becomes completely dormant
    /// </summary>
    public DeepSleepOptions DeepSleep { get; set; } = new();

    /// <summary>
    /// Timing configuration for natural, human-like behavior
    /// </summary>
    public TimingOptions Timing { get; set; } = new();

    /// <summary>
    /// Engagement thresholds and probabilities
    /// </summary>
    public EngagementOptions Engagement { get; set; } = new();

    /// <summary>
    /// Initiative action weights - higher weight = more likely to be chosen
    /// </summary>
    public ActionWeights ActionWeights { get; set; } = new();
}

/// <summary>
/// Configuration for deep sleep mode when the server is inactive
/// </summary>
public class DeepSleepOptions
{
    /// <summary>
    /// Whether deep sleep mode is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours of complete inactivity before entering deep sleep (no initiative actions)
    /// Default: 24 hours (1 day)
    /// </summary>
    public int InactivityHoursThreshold { get; set; } = 24;

    /// <summary>
    /// After deep sleep, require this many messages before Amiquin can initiate again
    /// This prevents the bot from immediately jumping into conversation after a long silence
    /// </summary>
    public int WakeUpMessageThreshold { get; set; } = 3;

    /// <summary>
    /// Hours after deep sleep before full initiative is restored
    /// During this period, initiative probability is reduced
    /// </summary>
    public int GradualWakeUpHours { get; set; } = 2;

    /// <summary>
    /// Initiative probability multiplier during gradual wake-up period (0.0 to 1.0)
    /// </summary>
    public float WakeUpProbabilityMultiplier { get; set; } = 0.3f;

    /// <summary>
    /// Whether to send a wake-up greeting when naturally coming out of deep sleep
    /// </summary>
    public bool SendWakeUpGreeting { get; set; } = false;
}

/// <summary>
/// Timing configuration for natural, human-like delays
/// </summary>
public class TimingOptions
{
    /// <summary>
    /// Minimum delay in seconds before responding to activity (feels less robotic)
    /// </summary>
    public int MinResponseDelaySeconds { get; set; } = 2;

    /// <summary>
    /// Maximum delay in seconds before responding to activity
    /// </summary>
    public int MaxResponseDelaySeconds { get; set; } = 15;

    /// <summary>
    /// Minimum minutes between initiative actions in the same server
    /// Prevents the bot from being too chatty
    /// </summary>
    public int MinMinutesBetweenInitiatives { get; set; } = 15;

    /// <summary>
    /// Maximum minutes between initiative actions (when activity is low but not deep sleep)
    /// </summary>
    public int MaxMinutesBetweenInitiatives { get; set; } = 120;

    /// <summary>
    /// Hours during which Amiquin is more likely to initiate (simulates being "awake")
    /// Uses server's local time if available, otherwise UTC
    /// </summary>
    public ActiveHoursOptions ActiveHours { get; set; } = new();
}

/// <summary>
/// Configuration for active hours (when Amiquin is more likely to engage)
/// </summary>
public class ActiveHoursOptions
{
    /// <summary>
    /// Whether to use active hours restrictions
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Start hour for active period (0-23, local time)
    /// </summary>
    public int StartHour { get; set; } = 9;

    /// <summary>
    /// End hour for active period (0-23, local time)
    /// </summary>
    public int EndHour { get; set; } = 23;

    /// <summary>
    /// Initiative probability multiplier during inactive hours (0.0 to 1.0)
    /// </summary>
    public float InactiveHoursMultiplier { get; set; } = 0.2f;
}

/// <summary>
/// Engagement thresholds and probability settings
/// </summary>
public class EngagementOptions
{
    /// <summary>
    /// Base probability for initiative action when activity is normal (0.0 to 1.0)
    /// </summary>
    public float BaseProbability { get; set; } = 0.15f;

    /// <summary>
    /// Maximum probability cap for initiative actions (0.0 to 1.0)
    /// </summary>
    public float MaxProbability { get; set; } = 0.5f;

    /// <summary>
    /// Minimum messages in context before considering initiative
    /// </summary>
    public int MinContextMessages { get; set; } = 2;

    /// <summary>
    /// Maximum consecutive initiative actions before requiring user interaction
    /// Prevents the bot from dominating the conversation
    /// </summary>
    public int MaxConsecutiveInitiatives { get; set; } = 2;

    /// <summary>
    /// How much to reduce probability after each consecutive initiative (0.0 to 1.0)
    /// </summary>
    public float ConsecutiveReductionFactor { get; set; } = 0.5f;

    /// <summary>
    /// Activity level thresholds for adjusting initiative behavior
    /// </summary>
    public ActivityThresholds ActivityThresholds { get; set; } = new();
}

/// <summary>
/// Activity level thresholds that affect initiative behavior
/// </summary>
public class ActivityThresholds
{
    /// <summary>
    /// Activity level below which initiative is more aggressive (server seems dead)
    /// </summary>
    public float LowActivityThreshold { get; set; } = 0.3f;

    /// <summary>
    /// Activity level above which initiative is reduced (let humans talk)
    /// </summary>
    public float HighActivityThreshold { get; set; } = 1.5f;

    /// <summary>
    /// Probability multiplier for low activity periods (0.0 to 2.0)
    /// </summary>
    public float LowActivityMultiplier { get; set; } = 1.5f;

    /// <summary>
    /// Probability multiplier for high activity periods (0.0 to 1.0)
    /// </summary>
    public float HighActivityMultiplier { get; set; } = 0.2f;
}

/// <summary>
/// Weights for different initiative actions (higher = more likely)
/// </summary>
public class ActionWeights
{
    /// <summary>
    /// Weight for starting a new topic
    /// </summary>
    public float StartTopic { get; set; } = 1.0f;

    /// <summary>
    /// Weight for asking an engaging question
    /// </summary>
    public float AskQuestion { get; set; } = 1.5f;

    /// <summary>
    /// Weight for sharing interesting content
    /// </summary>
    public float ShareInteresting { get; set; } = 0.8f;

    /// <summary>
    /// Weight for sharing funny content
    /// </summary>
    public float ShareFunny { get; set; } = 1.2f;

    /// <summary>
    /// Weight for sharing useful tips
    /// </summary>
    public float ShareUseful { get; set; } = 0.6f;

    /// <summary>
    /// Weight for increasing engagement
    /// </summary>
    public float IncreaseEngagement { get; set; } = 1.0f;

    /// <summary>
    /// Weight for sharing opinions
    /// </summary>
    public float ShareOpinion { get; set; } = 0.9f;

    /// <summary>
    /// Weight for adaptive responses (AI decides)
    /// </summary>
    public float AdaptiveResponse { get; set; } = 2.0f;
}
