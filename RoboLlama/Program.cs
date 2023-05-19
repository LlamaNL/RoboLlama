using RoboLlama;
using RoboLlama.Models;
using RoboLlama.Services;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((context, services) =>
{
    IConfiguration config = context.Configuration;
    services.Configure<ServerConfig>(config.GetSection(ServerConfig.Key));
    services.AddSingleton<IPluginService, PluginService>();
    services.AddHostedService<Bot>();
});

IHost host = builder.Build();

host.Run();
