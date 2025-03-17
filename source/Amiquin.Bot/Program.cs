using Amiquin.Bot.Configurators;
using Amiquin.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Sinks.SystemConsole.Themes;

Serilog.Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

var configurationManager = new ConfigurationManager()
    .AddEnvironmentVariables()
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
           .AddRepositories();
    })
    .UseSerilog((context, services, config) =>
    {
        var logsPath = context.Configuration.GetValue<string>(Constants.LogsPath);
        if (string.IsNullOrEmpty(logsPath))
            logsPath = AppContext.BaseDirectory;

        config.WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information, theme: AnsiConsoleTheme.Code)
            .WriteTo.File(Path.Combine(logsPath, "logs/log.log"), rollingInterval: RollingInterval.Day)
            .Enrich.FromLogContext()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services);
    })
    .Build();

await host.RunAsync();