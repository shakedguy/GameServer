using System.Data.Common;
using System.Text.Json;
using GameServer;
using GameServer.Configs;
using GameServer.Data;
using GameServer.Routes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Shared.Models;


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

builder.ConfigureServices(
    (context, services) =>
    {
        services.Configure<ServerSettings>(
            configs.GetRequiredSection(nameof(ServerSettings)));
        services.Configure<DbSettings>(
            configs.GetRequiredSection(nameof(DbSettings)));
        services.AddLogging(loggings =>
        {
            loggings.ClearProviders();
            loggings.AddSerilog(new LoggerConfiguration()
                .ReadFrom.Configuration(configs)
                .CreateLogger());
        });
        services.AddSingleton<DbDataSource>((serviceProvider) =>
        {
            var dbSettings =
                serviceProvider.GetRequiredService<IOptions<DbSettings>>();
            return SqliteFactory.Instance.CreateDataSource(dbSettings.Value
                .ConnectionString);
        });
        services.AddSingleton<IDbClient, DbClient>();
        services.AddTransient<IRepository<Player>, PlayersRepository>();
        services
            .AddTransient<IRepository<PlayerState>, PlayerStateRepository>();
        services.AddTransient<IRoute, LoginRoute>();
        services.AddTransient<IRoute,SendGiftRoute>();
        services.AddTransient<IRoute,UpdateResourcesRoute>();
        services.AddTransient<Router>();
        services.AddSingleton<App>();
    });


using var host = builder.Build();
using var app = host.Services.GetRequiredService<App>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();


lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine();
    logger.LogInformation("Server shutting down, Goodbye!");
});

Console.CancelKeyPress += (sender, eventArgs) =>
{
    if (eventArgs.SpecialKey == ConsoleSpecialKey.ControlC)
    {
        lifetime.StopApplication();
    }
};

await app.RunAsync(lifetime.ApplicationStopped);

