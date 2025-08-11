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
    /// In-memory dictionary of fun statistics (not mapped to database).
    /// </summary>
    [NotMapped]
    private Dictionary<string, object>? _funStatsCache;

    /// <summary>
    /// Gets the fun statistics as a dictionary.
    /// </summary>
    [NotMapped]
    public Dictionary<string, object> FunStats
    {
        get
        {
            if (_funStatsCache == null)
            {
                try
                {
                    _funStatsCache = JsonSerializer.Deserialize<Dictionary<string, object>>(FunStatsJson) 
                                    ?? new Dictionary<string, object>();
                }
                catch
                {
                    _funStatsCache = new Dictionary<string, object>();
                }
            }
            return _funStatsCache;
        }
        set
        {
            _funStatsCache = value;
            FunStatsJson = JsonSerializer.Serialize(value);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets a fun stat value of a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="defaultValue">Default value if stat doesn't exist.</param>
    /// <returns>The stat value or default value.</returns>
    public override T GetStat<T>(string statName, T defaultValue = default!)
    {
        if (!FunStats.TryGetValue(statName, out var value))
            return defaultValue;

        try
        {
            if (value is JsonElement jsonElement)
            {
                // Handle JsonElement deserialization
                return jsonElement.Deserialize<T>() ?? defaultValue;
            }
            
            // Direct cast for simple types
            return (T)Convert.ChangeType(value, typeof(T)) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Sets a fun stat value.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="value">The value to set.</param>
    public override void SetStat(string statName, object value)
    {
        FunStats[statName] = value;
        FunStatsJson = JsonSerializer.Serialize(FunStats);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments a numeric fun stat.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="increment">The amount to increment (default 1).</param>
    /// <returns>The new value after increment.</returns>
    public override int IncrementStat(string statName, int increment = 1)
    {
        var currentValue = GetStat<int>(statName, 0);
        var newValue = currentValue + increment;
        SetStat(statName, newValue);
        return newValue;
    }

    /// <summary>
    /// Checks if a fun stat exists.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <returns>True if the stat exists.</returns>
    public override bool HasStat(string statName)
    {
        return FunStats.ContainsKey(statName);
    }

    /// <summary>
    /// Unique constraint to ensure one record per user per server.
    /// </summary>
    [NotMapped]
    public string UniqueKey => $"{UserId}_{ServerId}";
}