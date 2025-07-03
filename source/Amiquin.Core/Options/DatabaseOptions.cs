namespace Amiquin.Core.Options;

public class DatabaseOptions : IOption
{
    public const string Database = "Database";
    public int Mode { get; set; } = default!;
    public string ConnectionString { get; set; } = default!;
    public string SQLitePath { get; set; } = default!;
    public string LogsPath { get; set; } = default!;
}