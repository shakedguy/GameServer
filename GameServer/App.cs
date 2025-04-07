using System.Net;
using GameServer.Configs;
using GameServer.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameServer;

public class App : IDisposable, IAsyncDisposable
{
    private readonly ILogger<App> _logger;
    private readonly Router _router;
    private readonly IDbClient _dbClient;
    private readonly HttpListener _server;
    private readonly ServerSettings _settings;

    public App(IOptions<ServerSettings> options, ILogger<App> logger,
        Router router, IDbClient dbClient)
    {
        _logger = logger;
        _router = router;
        _dbClient = dbClient;
        _settings = options.Value;
        _server = new HttpListener { Prefixes = { _settings.Url } };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _dbClient.Initialize();
        _server.Start();
        _logger.LogInformation("Game Server started on {Url}", _settings.Url);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _server.GetContextAsync();
                _ = HandleClientAsync(context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
        }
        finally
        {
            StopServer();
            _logger.LogInformation("Game Server stopped");
        }
    }

    private async Task HandleClientAsync(HttpListenerContext context)
    {
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var wsContext = await context.AcceptWebSocketAsync(null);
        _ = _router.Handle(wsContext.WebSocket);
    }

    private void StopServer()
    {
        if (_server.IsListening)
        {
            _server.Stop();
            _server.Close();
        }
    }

    public ValueTask DisposeAsync()
    {
        StopServer();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => StopServer();
}