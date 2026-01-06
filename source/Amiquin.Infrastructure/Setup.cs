using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Amiquin.Infrastructure;

public static class Setup
{
    private const string DefaultSqliteDbPath = "Data/Database/amiquin.db";

    /// <summary>
    /// Adds the SQLite database context to the service collection.
    /// Uses Database:ConnectionString if provided, otherwise defaults to Data/Database/amiquin.db
    /// </summary>
    public static IServiceCollection AddAmiquinContext(this IServiceCollection services, IConfiguration configuration)
    {
        var dbOptions = configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var connectionString = dbOptions?.ConnectionString ?? $"Data Source={DefaultSqliteDbPath}";

        // Resolve and normalize the database path
        connectionString = ResolveAndNormalizeSqlitePath(connectionString);

        Log.Information("Using SQLite database at {ConnectionString}", connectionString);
        Log.Information("Using migration assembly {MigrationAssembly}", Constants.MigrationAssemblies.SQLite);

        services.AddDbContext<AmiquinContext>(options =>
        {
            options.UseSqlite(connectionString, x => x.MigrationsAssembly(Constants.MigrationAssemblies.SQLite));
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }

    /// <summary>
    /// Adds the MySQL database context to the service collection.
    /// Requires Database:ConnectionString to be set.
    /// </summary>
    public static IServiceCollection AddAmiquinMySqlContext(this IServiceCollection services, IConfiguration configuration)
    {
        var dbOptions = configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var connectionString = dbOptions?.ConnectionString
            ?? throw new InvalidOperationException(
                "MySQL connection string is required. Set 'Database:ConnectionString' in configuration or AMQ_Database__ConnectionString environment variable.");

        Log.Information("Using MySQL database at {ConnectionString}", StringModifier.Anomify(connectionString));
        Log.Information("Using migration assembly {MigrationAssembly}", Constants.MigrationAssemblies.MySQL);

        services.AddDbContext<AmiquinContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(Constants.MigrationAssemblies.MySQL));
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }

    /// <summary>
    /// Resolves relative paths in SQLite connection strings and ensures the directory exists.
    /// </summary>
    private static string ResolveAndNormalizeSqlitePath(string connectionString)
    {
        const string dataSourcePrefix = "Data Source=";
        var pathStartIndex = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);

        if (pathStartIndex < 0)
            return connectionString;

        var pathStart = pathStartIndex + dataSourcePrefix.Length;
        var pathEndIndex = connectionString.IndexOf(';', pathStart);

        if (pathEndIndex < 0)
            pathEndIndex = connectionString.Length;

        if (pathEndIndex <= pathStart)
            return connectionString;

        var dbPath = connectionString.Substring(pathStart, pathEndIndex - pathStart).Trim();

        // Only resolve if path is relative
        if (Path.IsPathRooted(dbPath))
            return connectionString;

        // Resolve relative path against AppContext.BaseDirectory
        var absolutePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dbPath));

        // Ensure directory exists
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Rebuild connection string with absolute path
        var beforePath = connectionString.Substring(0, pathStart);
        var afterPath = pathEndIndex < connectionString.Length ? connectionString.Substring(pathEndIndex) : "";
        return beforePath + absolutePath + afterPath;
    }
}