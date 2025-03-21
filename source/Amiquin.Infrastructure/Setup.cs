using Amiquin.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        var connectionString = configuration.GetValue<string>(Constants.Environment.SQLitePath) ?? $"Data Source={Path.Join(AppContext.BaseDirectory, "data.db")}";
        services.AddDbContext<AmiquinContext>(options =>
            options.UseSqlite(connectionString, x => x.MigrationsAssembly(typeof(AmiquinContext).Assembly.GetName().Name)));

        return services;
    }
}