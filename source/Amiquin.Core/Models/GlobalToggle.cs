using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

/// <summary>
/// Represents a global feature toggle that applies system-wide.
/// Server toggles can override these defaults if configured.
/// </summary>
public class GlobalToggle : DbModel<string>
{
    /// <summary>
    /// Unique identifier for the toggle (typically the toggle name).
    /// </summary>
    public override string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the toggle (e.g., "EnableChat", "EnableTTS").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the toggle is enabled globally.
    /// This is the default value when no server override exists.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Description of what this toggle controls.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When this toggle was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this toggle was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether servers can override this toggle.
    /// If false, the global setting always applies.
    /// </summary>
    public bool AllowServerOverride { get; set; } = true;

    /// <summary>
    /// Category for organizing toggles in the UI (e.g., "Chat", "Voice", "Fun").
    /// </summary>
    public string Category { get; set; } = "General";
}
