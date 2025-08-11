using Amiquin.Core.Abstraction;
using System.Text.Json;

namespace Amiquin.Core.Models;

/// <summary>
/// Core domain model for user statistics with dynamic JSON-based stats storage.
/// </summary>
public class UserStats : DbModel<int>
{
    /// <summary>
    /// Discord user ID.
    /// </summary>
    public virtual ulong UserId { get; set; }

    /// <summary>
    /// Discord server ID.
    /// </summary>
    public virtual ulong ServerId { get; set; }

    /// <summary>
    /// When this record was created.
    /// </summary>
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this record was last updated.
    /// </summary>
    public virtual DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the JSON representation of fun statistics.
    /// This is implemented in the Infrastructure layer.
    /// </summary>
    protected virtual string GetStatsJson() => "{}";
    
    /// <summary>
    /// Sets the JSON representation of fun statistics.
    /// This is implemented in the Infrastructure layer.
    /// </summary>
    protected virtual void SetStatsJson(string json) { }

    /// <summary>
    /// In-memory cache of fun statistics.
    /// </summary>
    private Dictionary<string, object>? _statsCache;

    /// <summary>
    /// Gets the stats dictionary, loading from JSON if needed.
    /// </summary>
    protected Dictionary<string, object> Stats
    {
        get
        {
            if (_statsCache == null)
            {
                try
                {
                    _statsCache = JsonSerializer.Deserialize<Dictionary<string, object>>(GetStatsJson()) 
                                  ?? new Dictionary<string, object>();
                }
                catch
                {
                    _statsCache = new Dictionary<string, object>();
                }
            }
            return _statsCache;
        }
    }

    /// <summary>
    /// Gets a fun stat value of a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="defaultValue">Default value if stat doesn't exist.</param>
    /// <returns>The stat value or default value.</returns>
    public virtual T GetStat<T>(string statName, T defaultValue = default!)
    {
        if (!Stats.TryGetValue(statName, out var value))
            return defaultValue;

        try
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>() ?? defaultValue;
            }
            
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
    public virtual void SetStat(string statName, object value)
    {
        Stats[statName] = value;
        var newJson = JsonSerializer.Serialize(Stats);
        SetStatsJson(newJson);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments a numeric fun stat.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="increment">The amount to increment (default 1).</param>
    /// <returns>The new value after increment.</returns>
    public virtual int IncrementStat(string statName, int increment = 1)
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
    public virtual bool HasStat(string statName)
    {
        return Stats.ContainsKey(statName);
    }
}