

using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Serilog;

namespace Amiquin.Infrastructure;

public static class Setup
{
    /// <summary>
    /// Adds the SQLite database context to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The application configuration from which to read the connection string.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAmiquinContext(this IServiceCollection services, IConfiguration configuration)
    {
        // Priority order: Provider-specific env var -> Legacy env var -> Configuration section -> Default
        var connectionString = configuration.GetValue<string>(Constants.Environment.AmiquinSqliteConnectionString) ??
                               configuration.GetValue<string>(Constants.Environment.SQLitePath) ??
                               configuration.GetConnectionString("Amiquin-Sqlite") ??
                               configuration.GetConnectionString("AmiquinContext") ??
                               Constants.DefaultValues.DefaultSQLiteConnectionString;

        var migrationAssembly = Constants.MigrationAssemblies.SQLite;
        // Parse and resolve relative paths in SQLite connection string
        var dataSourcePrefix = "Data Source=";
        var pathStartIndex = connectionString.IndexOf(dataSourcePrefix, StringComparison.OrdinalIgnoreCase);
        
        if (pathStartIndex >= 0)
        {
            var pathStart = pathStartIndex + dataSourcePrefix.Length;
            var pathEndIndex = connectionString.IndexOf(';', pathStart);
            
            // If no semicolon found, assume the path extends to the end of the string
            if (pathEndIndex < 0)
                pathEndIndex = connectionString.Length;
            
            if (pathEndIndex > pathStart)
            {
                var dbPath = connectionString.Substring(pathStart, pathEndIndex - pathStart).Trim();
                
                // Check if the path is relative (not rooted)
                if (!Path.IsPathRooted(dbPath))
                {
                    // Resolve relative path against AppContext.BaseDirectory and normalize
                    var combinedPath = Path.Combine(AppContext.BaseDirectory, dbPath);
                    var absolutePath = Path.GetFullPath(combinedPath);
                    
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(absolutePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Rebuild connection string with absolute path
                    var beforePath = connectionString.Substring(0, pathStart);
                    var afterPath = pathEndIndex < connectionString.Length ? connectionString.Substring(pathEndIndex) : "";
                    connectionString = beforePath + absolutePath + afterPath;
                }
            }
        }

        Log.Information("Using SQLite database at {ConnectionString}", connectionString);
        Log.Information("Using migration assembly {MigrationAssembly}", migrationAssembly);

        services.AddDbContext<AmiquinContext>(options =>
        {
            options.UseSqlite(connectionString, x => x.MigrationsAssembly(migrationAssembly));
            // Temporarily suppress pending model changes warning until migrations are generated
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }

    /// <summary>
    /// Adds the MySQL database context to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">The application configuration from which to read the connection string.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAmiquinMySqlContext(this IServiceCollection services, IConfiguration configuration)
    {
        // Priority order: Provider-specific env var -> Legacy env var -> Configuration section -> Default
        var dbConfig = configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var connectionString = configuration.GetValue<string>(Constants.Environment.AmiquinMysqlConnectionString) ??
                               configuration.GetValue<string>(Constants.Environment.DbConnectionString) ??
                               configuration.GetConnectionString("Amiquin-Mysql") ??
                               configuration.GetConnectionString("AmiquinContext") ??
                               dbConfig?.ConnectionString ??
                               Constants.DefaultValues.DefaultMySQLConnectionString;

        var migrationAssembly = Constants.MigrationAssemblies.MySQL;
        Log.Information("Using MySQL database at {ConnectionString}", StringModifier.Anomify(connectionString));
        Log.Information("Using migration assembly {MigrationAssembly}", migrationAssembly);

        services.AddDbContext<AmiquinContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysql => mysql.MigrationsAssembly(migrationAssembly));
            // Temporarily suppress pending model changes warning until migrations are generated  
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        return services;
    }
}