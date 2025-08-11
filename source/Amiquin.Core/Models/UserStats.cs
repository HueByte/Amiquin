using Amiquin.Core.Abstraction;

namespace Amiquin.Core.Models;

/// <summary>
/// Core domain model for user statistics.
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
    /// Gets a fun stat value of a specific type.
    /// </summary>
    /// <typeparam name="T">The type to cast the value to.</typeparam>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="defaultValue">Default value if stat doesn't exist.</param>
    /// <returns>The stat value or default value.</returns>
    public virtual T GetStat<T>(string statName, T defaultValue = default!)
    {
        // This will be implemented by the Infrastructure layer
        throw new NotImplementedException("This method should be overridden by the Infrastructure layer");
    }

    /// <summary>
    /// Sets a fun stat value.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="value">The value to set.</param>
    public virtual void SetStat(string statName, object value)
    {
        // This will be implemented by the Infrastructure layer
        throw new NotImplementedException("This method should be overridden by the Infrastructure layer");
    }

    /// <summary>
    /// Increments a numeric fun stat.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <param name="increment">The amount to increment (default 1).</param>
    /// <returns>The new value after increment.</returns>
    public virtual int IncrementStat(string statName, int increment = 1)
    {
        // This will be implemented by the Infrastructure layer
        throw new NotImplementedException("This method should be overridden by the Infrastructure layer");
    }

    /// <summary>
    /// Checks if a fun stat exists.
    /// </summary>
    /// <param name="statName">The name of the stat.</param>
    /// <returns>True if the stat exists.</returns>
    public virtual bool HasStat(string statName)
    {
        // This will be implemented by the Infrastructure layer
        throw new NotImplementedException("This method should be overridden by the Infrastructure layer");
    }
}