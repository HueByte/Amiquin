using Amiquin.Core.IRepositories;
using Amiquin.Core.Options;
using Amiquin.Core.Services.ChatSession;
using Amiquin.Core.Services.MessageCache;
using Amiquin.Core.Services.Meta;
using Amiquin.Core.Services.Nacho;
using Amiquin.Core.Services.Toggle;
using Amiquin.Infrastructure;
using Amiquin.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Amiquin.IntegrationTests.Fixtures;

public class DatabaseFixture : IDisposable, IAsyncLifetime
{
    public IServiceProvider ServiceProvider { get; private set; }
    public AmiquinContext DbContext { get; private set; }
    private readonly string _databaseName;

    public DatabaseFixture()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
        var services = new ServiceCollection();

        // Configure in-memory database for testing with unique name per fixture
        services.AddDbContext<AmiquinContext>(options =>
            options.UseInMemoryDatabase(databaseName: _databaseName));

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
        services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

        // Register services
        services.AddScoped<IServerMetaService, ServerMetaService>();
        services.AddScoped<IToggleService, ToggleService>();
        services.AddScoped<INachoService, NachoService>();
        services.AddScoped<IMessageCacheService, MessageCacheService>();
        services.AddScoped<IChatSessionService, ChatSessionService>();

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
        // Remove child entities first to avoid cascade delete issues
        DbContext.Messages.RemoveRange(DbContext.Messages);
        DbContext.Toggles.RemoveRange(DbContext.Toggles);
        DbContext.NachoPacks.RemoveRange(DbContext.NachoPacks);
        if (DbContext.ChatSessions != null)
            DbContext.ChatSessions.RemoveRange(DbContext.ChatSessions);
        if (DbContext.CommandLogs != null)
            DbContext.CommandLogs.RemoveRange(DbContext.CommandLogs);

        // Remove parent entities last
        DbContext.ServerMetas.RemoveRange(DbContext.ServerMetas);

        await DbContext.SaveChangesAsync();
    }

    public async Task InitializeAsync()
    {
        // Clean database on initialization
        await CleanupAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up database before disposal
        await CleanupAsync();
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        ServiceProvider?.GetService<IServiceScope>()?.Dispose();
    }
}