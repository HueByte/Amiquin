using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Amiquin.Infrastructure.Entities;

/// <summary>
/// Represents dynamic user statistics stored per server.
/// Uses JSON storage for extensible fun stats without requiring migrations.
/// </summary>
[Table("user_stats")]
public class UserStats : Core.Models.UserStats
{
    /// <summary>
    /// Primary key for the user stats record.
    /// </summary>
    [Key]
    [Column("id")]
    public override int Id { get; set; }

    /// <summary>
    /// Discord user ID.
    /// </summary>
    [Required]
    [Column("user_id")]
    public override ulong UserId { get; set; }

    /// <summary>
    /// Discord server ID.
    /// </summary>
    [Required]
    [Column("server_id")]
    public override ulong ServerId { get; set; }

    /// <summary>
    /// JSON-serialized dictionary of fun statistics.
    /// Key: stat name (e.g., "dick_size", "nachos_given", "hugs_received")
    /// Value: stat value as object (can be int, string, etc.)
    /// </summary>
    [Column("fun_stats", TypeName = "TEXT")]
    public string FunStatsJson { get; set; } = "{}";

    /// <summary>
    /// When this record was created.
    /// </summary>
    [Column("created_at")]
    public override DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    [Column("updated_at")]
    public override DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the JSON representation of fun statistics.
    /// </summary>
    protected override string GetStatsJson() => FunStatsJson;
    
    /// <summary>
    /// Sets the JSON representation of fun statistics.
    /// </summary>
    protected override void SetStatsJson(string json) => FunStatsJson = json;

    /// <summary>
    /// Unique constraint to ensure one record per user per server.
    /// </summary>
    [NotMapped]
    public string UniqueKey => $"{UserId}_{ServerId}";
}