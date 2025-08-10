using Amiquin.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Amiquin.Infrastructure;

/// <summary>
/// Design-time factory for AmiquinContext - primarily for runtime configuration
/// Migration-specific factories are located in each migration project
/// </summary>
public class AmiquinContextDesignTimeFactory : IDesignTimeDbContextFactory<AmiquinContext>
{
    public AmiquinContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AmiquinContext>();

        // Default to SQLite for general design-time operations
        // Migration projects have their own dedicated factories
        optionsBuilder.UseSqlite(
            "Data Source=design_time.db",
            options => options.MigrationsAssembly("Amiquin.Sqlite")
        );

        // Enable logging for design-time
        optionsBuilder.EnableSensitiveDataLogging();
        optionsBuilder.EnableDetailedErrors();

        return new AmiquinContext(optionsBuilder.Options);
    }
}
