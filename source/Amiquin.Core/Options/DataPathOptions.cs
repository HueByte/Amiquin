namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for data storage paths.
/// </summary>
public class DataPathOptions
{
    public const string SectionName = "DataPaths";
    
    /// <summary>
    /// Path for log files.
    /// </summary>
    public string Logs { get; set; } = "Data/Logs";
    
    /// <summary>
    /// Path for message storage.
    /// </summary>
    public string Messages { get; set; } = "Data/Messages";
    
    /// <summary>
    /// Path for session data.
    /// </summary>
    public string Sessions { get; set; } = "Data/Sessions";
    
    /// <summary>
    /// Path for plugin data.
    /// </summary>
    public string Plugins { get; set; } = "Data/Plugins";
    
    /// <summary>
    /// Path for configuration files.
    /// </summary>
    public string Configuration { get; set; } = "Configuration";
    
    /// <summary>
    /// Gets the full absolute path for a relative path.
    /// </summary>
    /// <param name="relativePath">The relative path to resolve.</param>
    /// <returns>The full absolute path.</returns>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
    }
}