using RoboLlama;
using RoboLlama.Models;
using RoboLlama.Services;
using Serilog.Events;
using Serilog;

string LogFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "RoboLlama.log");
var fileInfo = new FileInfo(LogFolder);
fileInfo.Directory!.Create();

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(LogFolder, LogEventLevel.Error, fileSizeLimitBytes: 20000, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true, retainedFileCountLimit: 31)
            .CreateLogger();

IHostBuilder builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices((context, services) =>
{
    IConfiguration config = context.Configuration;
    services.Configure<ServerConfig>(config.GetSection(ServerConfig.Key));
    services.AddSingleton<IPluginService, PluginService>();
    services.AddHostedService<Bot>();
    services.AddWindowsService(options => options.ServiceName = "RoboLlama");
});

IHost host = builder
.UseSerilog()
.Build();

host.Run();
