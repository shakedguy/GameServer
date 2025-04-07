using System.Data.Common;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GameClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;


var configs = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args)
    .Build();

var builder = Host.CreateDefaultBuilder(args);
builder.ConfigureHostConfiguration((config) =>
    config.AddConfiguration(configs));

builder.ConfigureAppConfiguration((_, config) =>
    config.AddConfiguration(configs));

var ws = new ClientWebSocket();

builder.ConfigureServices(
    (context, services) =>
    {
        services.Configure<ClientSettings>(
            configs.GetRequiredSection(nameof(ClientSettings)));

        services.AddLogging(loggings =>
        {
            loggings.ClearProviders();
            loggings.AddSerilog(new LoggerConfiguration()
                .ReadFrom.Configuration(context.Configuration)
                .CreateLogger());
        });
        services.AddSingleton(ws);

        services.AddSingleton<App>();
    });


var host = builder.Build();

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlC)
    {
        cancellationTokenSource.Cancel();
    }
};
var app = host.Services.GetRequiredService<App>();
await app.RunAsync(cancellationTokenSource.Token);