namespace Amiquin.Core.Options.Configuration;

/// <summary>
/// Configuration options for session management.
/// </summary>
public class SessionManagementOptions
{
    public const string SectionName = "SessionManagement";
    
    /// <summary>
    /// Maximum number of sessions allowed per user.
    /// </summary>
    public int MaxSessionsPerUser { get; set; } = 5;
    
    /// <summary>
    /// Session inactivity timeout in minutes.
    /// </summary>
    public int InactivityTimeoutMinutes { get; set; } = 120;
    
    /// <summary>
    /// Cleanup interval for inactive sessions in minutes.
    /// </summary>
    public int CleanupIntervalMinutes { get; set; } = 30;
    
    /// <summary>
    /// Maximum number of messages to keep in history.
    /// </summary>
    public int MaxHistoryLength { get; set; } = 50;
}