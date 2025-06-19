using Amiquin.Bot.Configurators;
using Amiquin.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;

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
        Serilog.Log.Logger.Information("Bot is restarting");
    }
    catch (Exception ex)
    {
        failCount++;
        Serilog.Log.Logger.Error(ex, "Error appeared during bot execution");

        if (failCount >= maxFailCount)
        {
            Serilog.Log.Logger.Error("Maximum fail count reached. Exiting...");
            Shutdown(); // Cancel the shutdown token
            break;
        }
    }

    await Task.Delay(2000);
}

await tcs.Task;

async Task RunAsync(string[] args)
{
    Serilog.Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .Enrich.FromLogContext()
            .CreateBootstrapLogger();

    var configurationManager = new ConfigurationManager()
        .AddEnvironmentVariables(prefix: "AMQ_")
        .AddJsonFile("appsettings.json", optional: false)
        .AddCommandLine(args);

    var logger = new SerilogLoggerProvider(Serilog.Log.Logger)
         .CreateLogger(nameof(Program));

    var hostBuilder = Host.CreateDefaultBuilder(args);
    var host = hostBuilder.ConfigureHostConfiguration(host =>
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
            var logsPath = context.Configuration.GetValue<string>(Constants.Environment.LogsPath);
            if (string.IsNullOrEmpty(logsPath))
                logsPath = AppContext.BaseDirectory;

            config.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, theme: AnsiConsoleTheme.Code)
                .WriteTo.File(Path.Combine(logsPath, "logs/log.log"), rollingInterval: RollingInterval.Day)
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Verbose)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Verbose)
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);
        })
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
