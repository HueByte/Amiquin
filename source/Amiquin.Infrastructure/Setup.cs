

using Amiquin.Core;
using Amiquin.Core.Options;
using Amiquin.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var connectionString = configuration.GetValue<string>(Constants.Environment.SQLitePath) ?? $"Data Source={Path.Join(AppContext.BaseDirectory, Constants.DefaultValues.DefaultSQLiteDatabase)}";
        var migrationAssembly = Constants.MigrationAssemblies.SQLite;
        Log.Information("Using SQLite database at {ConnectionString}", connectionString);
        Log.Information("Using migration assembly {MigrationAssembly}", migrationAssembly);

        services.AddDbContext<AmiquinContext>(options =>
            options.UseSqlite(connectionString, x => x.MigrationsAssembly(migrationAssembly)));

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
        var dbConfig = configuration.GetSection(DatabaseOptions.Database).Get<DatabaseOptions>();
        var connectionString = configuration.GetValue<string>(Constants.Environment.DbConnectionString) ?? dbConfig?.ConnectionString ?? Constants.DefaultValues.DefaultMySQLConnectionString;
        var migrationAssembly = Constants.MigrationAssemblies.MySQL;
        Log.Information("Using MySQL database at {ConnectionString}", StringModifier.Anomify(connectionString));
        Log.Information("Using migration assembly {MigrationAssembly}", migrationAssembly);

        services.AddDbContext<AmiquinContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString), mysql => mysql.MigrationsAssembly(migrationAssembly)));

        return services;
    }
}