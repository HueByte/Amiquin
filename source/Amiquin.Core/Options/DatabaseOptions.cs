namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for database connections.
/// </summary>
public class DatabaseOptions : IOption
{
    public const string Database = "Database";

    /// <summary>
    /// Database mode: 0 = MySQL, 1 = SQLite (default).
    /// </summary>
    public int Mode { get; set; } = 1; // SQLite by default

    /// <summary>
    /// Optional connection string override. If not set, uses default path-based connection.
    /// For SQLite: "Data Source=path/to/database.db"
    /// For MySQL: "Server=host;Database=db;User=user;Password=pass;"
    /// </summary>
    public string? ConnectionString { get; set; }
}