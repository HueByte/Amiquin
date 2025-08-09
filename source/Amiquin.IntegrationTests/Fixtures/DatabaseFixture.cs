using Amiquin.Core.IRepositories;
using Amiquin.Core.Options;
using Amiquin.Core.Services.Toggle;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Nacho;
using Amiquin.Infrastructure;
using Amiquin.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Amiquin.IntegrationTests.Fixtures;

public class DatabaseFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; private set; }
    public AmiquinContext DbContext { get; private set; }

    public DatabaseFixture()
    {
        var services = new ServiceCollection();

        // Configure in-memory database for testing
        services.AddDbContext<AmiquinContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Add memory cache
        services.AddMemoryCache();

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["BotName"] = "TestBot",
                ["BotVersion"] = "1.0.0",
                ["MessageFetchCount"] = "40"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add options
        services.Configure<BotOptions>(options =>
        {
            options.MessageFetchCount = 40;
        });

        // Register repositories
        services.AddScoped<IServerMetaRepository, ServerMetaRepository>();
        services.AddScoped<IToggleRepository, ToggleRepository>();
        services.AddScoped<INachoRepository, NachoRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();

        // Register services
        services.AddScoped<IServerMetaService, ServerMetaService>();
        services.AddScoped<IToggleService, ToggleService>();
        services.AddScoped<INachoService, NachoService>();
        services.AddScoped<IMessageCacheService, MessageCacheService>();

        ServiceProvider = services.BuildServiceProvider();

        // Get DbContext and ensure it's created
        DbContext = ServiceProvider.GetRequiredService<AmiquinContext>();
        DbContext.Database.EnsureCreated();
    }

    public async Task SeedTestDataAsync()
    {
        // Clear existing data
        DbContext.ServerMetas.RemoveRange(DbContext.ServerMetas);
        DbContext.Toggles.RemoveRange(DbContext.Toggles);
        DbContext.NachoPacks.RemoveRange(DbContext.NachoPacks);
        DbContext.Messages.RemoveRange(DbContext.Messages);
        await DbContext.SaveChangesAsync();

        // Seed test data
        var serverMeta = new Core.Models.ServerMeta
        {
            Id = 123456789UL,
            ServerName = "Test Server",
            IsActive = true,
            Persona = "Test persona",
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            Toggles = new List<Core.Models.Toggle>(),
            Messages = new List<Core.Models.Message>(),
            CommandLogs = new List<Core.Models.CommandLog>(),
            NachoPacks = new List<Core.Models.NachoPack>()
        };

        DbContext.ServerMetas.Add(serverMeta);
        await DbContext.SaveChangesAsync();
    }

    public async Task CleanupAsync()
    {
        DbContext.ServerMetas.RemoveRange(DbContext.ServerMetas);
        DbContext.Toggles.RemoveRange(DbContext.Toggles);
        DbContext.NachoPacks.RemoveRange(DbContext.NachoPacks);
        DbContext.Messages.RemoveRange(DbContext.Messages);
        await DbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        ServiceProvider?.GetService<IServiceScope>()?.Dispose();
    }
}