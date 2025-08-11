namespace Amiquin.Core.Services.ComponentHandler;

/// <summary>
/// Context information parsed from a component's custom ID.
/// </summary>
public class ComponentContext
{
    /// <summary>
    /// The prefix that identifies the handler type.
    /// </summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>
    /// Additional parameters extracted from the custom ID.
    /// </summary>
    public string[] Parameters { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The original custom ID that was parsed.
    /// </summary>
    public string OriginalCustomId { get; init; } = string.Empty;

    /// <summary>
    /// Creates a new ComponentContext.
    /// </summary>
    /// <param name="prefix">The handler prefix.</param>
    /// <param name="parameters">The parsed parameters.</param>
    /// <param name="originalCustomId">The original custom ID.</param>
    public ComponentContext(string prefix, string[] parameters, string originalCustomId)
    {
        Prefix = prefix;
        Parameters = parameters;
        OriginalCustomId = originalCustomId;
    }

    /// <summary>
    /// Gets a parameter by index, returning null if not found.
    /// </summary>
    /// <param name="index">The parameter index.</param>
    /// <returns>The parameter value or null.</returns>
    public string? GetParameter(int index)
    {
        return index >= 0 && index < Parameters.Length ? Parameters[index] : null;
    }

    /// <summary>
    /// Gets a parameter by index and tries to parse it as the specified type.
    /// </summary>
    /// <typeparam name="T">The type to parse to.</typeparam>
    /// <param name="index">The parameter index.</param>
    /// <param name="defaultValue">The default value if parsing fails.</param>
    /// <returns>The parsed value or the default value.</returns>
    public T GetParameter<T>(int index, T defaultValue = default!) where T : IParsable<T>
    {
        var param = GetParameter(index);
        return param != null && T.TryParse(param, null, out var result) ? result : defaultValue;
    }
}