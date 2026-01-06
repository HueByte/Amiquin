using Amiquin.Bot.Configurators;
using Amiquin.Core.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

var tcs = new TaskCompletionSource();
var sigintReceived = false;
int failCount = 0;
const int maxFailCount = 5; // Maximum number of retries
Console.CancelKeyPress += (_, ea) =>
{
    // Tell .NET to not terminate the process
    ea.Cancel = true;

    Console.WriteLine("Received SIGINT (Ctrl+C)");
    sigintReceived = true;
    Shutdown(); // Cancel the shutdown token
    tcs.SetResult();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    if (!sigintReceived)
    {
        Console.WriteLine("Received SIGTERM");
        Shutdown(); // Cancel the shutdown token
        tcs.SetResult();
    }
    else
    {
        Console.WriteLine("Received SIGTERM, ignoring it because already processed SIGINT");
    }
};

while (!sigintReceived && !_shutdownTokenSource.IsCancellationRequested)
{
    try
    {
        await RunAsync(args);
        failCount = 0; // Reset fail count on successful execution
    }
    catch (OperationCanceledException)
    {
        Log.Logger.Information("Bot is restarting");
    }
    catch (HostAbortedException)
    {
        Log.Logger.Information("Host aborted, shutting down gracefully");
        Shutdown(); // Cancel the shutdown token
        break; // Exit the loop if the host is aborted
    }
    catch (DatabaseNotImplementedException)
    {
        Log.Logger.Error("Database not implemented for the current database mode. Please check your configuration.");
        Shutdown();
        break; // Exit the loop if the database is not implemented
    }
    catch (Exception ex)
    {
        failCount++;
        Log.Logger.Error(ex, "Error appeared during bot execution");

        if (failCount >= maxFailCount)
        {
            Log.Logger.Error("Maximum fail count reached. Exiting...");
            Shutdown(); // Cancel the shutdown token
            break;
        }
    }

    if (failCount != 0)
    {
        Log.Logger.Information("Restarting bot in 2 seconds...");
        await Task.Delay(2000);
    }
}

await tcs.Task;

// Ensure logs are flushed before application exits
Log.CloseAndFlush();

async Task RunAsync(string[] args)
{
    var basePath = AppContext.BaseDirectory;
    var configPath = Path.Combine(basePath, "Data", "Configuration");
    var appSettingsPath = Path.Combine(configPath, "appsettings.json");
    var exampleSettingsPath = Path.Combine(configPath, "appsettings.example.json");

    // If appsettings.json doesn't exist but example does, copy it
    if (!File.Exists(appSettingsPath) && File.Exists(exampleSettingsPath))
    {
        Directory.CreateDirectory(configPath);
        File.Copy(exampleSettingsPath, appSettingsPath);
        Console.WriteLine($"Created appsettings.json from example. Please configure your Discord token and API keys.");
    }

    var configurationManager = new ConfigurationManager()
        .SetBasePath(basePath)
        .AddJsonFile(appSettingsPath, optional: false)
        .AddEnvironmentVariables()
        .AddEnvironmentVariables(prefix: "AMQ_")
        .AddCommandLine(args);

    // Create a bootstrap logger for early initialization logging
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .CreateBootstrapLogger();

    var hostBuilder = Host.CreateDefaultBuilder(args);
    var host = hostBuilder
        .ConfigureHostConfiguration(host =>
        {
            host.AddConfiguration(configurationManager.Build());
        })
        .ConfigureServices((hostContext, services) =>
        {
            InjectionConfigurator ioc = new(hostContext.Configuration, services);

            ioc.AddAmiquinCore()
               .AddOptions()
               .AddServices()
               .AddRepositories()
               .AddRunnableJobs();
        })
        .UseSerilog((context, services, config) =>
        {
            // Configure Serilog from appsettings.json only
            config.ReadFrom.Configuration(context.Configuration)
                  .ReadFrom.Services(services);
        }, writeToProviders: false)
        .Build();

    await host.RunAsync(_restartBotTokenSource.Token);
}

public partial class Program
{
    private static CancellationTokenSource _shutdownTokenSource = new();
    private static CancellationTokenSource _restartBotTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownTokenSource.Token);
    public static void Restart()
    {
        _restartBotTokenSource.Cancel();
        _restartBotTokenSource = new();
    }

    public static void Shutdown()
    {
        _shutdownTokenSource.Cancel();
    }
}