using System.Net.WebSockets;
using System.Text;
using GameServer.Data;
using GameServer.Routes;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using AppContext = GameServer.Models.AppContext;

namespace GameServer;

public class Router
{
    private const int MESSAGE_SIZE = 4096;

    private readonly ILogger<Router> _logger;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<PlayerState> _playerStateRepository;


    private static readonly
        IDictionary<string, (string playerId, WebSocket socket)>
        _onlinePlayers = new Dictionary<string, (string, WebSocket)>();

    private readonly IDictionary<string,
            Func<AppContext, Task>>
        _routes = new Dictionary<string, Func<AppContext, Task>>();

    public Router(
        ILogger<Router> logger,
        IRepository<Player> playerRepository,
        IRepository<PlayerState> playerStateRepository,
        IEnumerable<IRoute> routes
    )
    {
        _logger = logger;
        _playerRepository = playerRepository;
        _playerStateRepository = playerStateRepository;

        foreach (var route in routes)
        {
            _routes[route.Event] = route.Handler;
        }
    }


    public async Task Handle(WebSocket socket)
    {
        var buffer = new byte[MESSAGE_SIZE];
        var state = new PlayerState();

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result =
                    await socket.ReceiveAsync(buffer, CancellationToken.None);
                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (Message.TryParse(json, out var message))
                {
                    _logger.LogInformation("Message received: {Message}",
                        Message.Parse(json));

                    await ProcessMessageAsync(socket, state, message);
                }
                else if (!string.IsNullOrEmpty(json))
                {
                    _logger.LogWarning("Unknow message {message}", json);
                    await socket.PublishAsync(new ErrorMessage(
                        "Invalid message",
                        400));
                    return;
                }
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode ==
                                            WebSocketError
                                                .ConnectionClosedPrematurely)
        {
            _logger.LogInformation(
                "Client closed connection prematurely: {Message}", ex.Message);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Unexpected WebSocket error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in WebSocket session");
        }
        finally
        {
            RemovePlayerFromOnlineList(state.PlayerId);
        }
    }

    private string FindOnlinePlayerDeviceId(string playerId)
    {
        var playerEntry =
            _onlinePlayers.FirstOrDefault(kv => kv.Value.playerId == playerId);

        return playerEntry.Key ?? string.Empty;
    }

    private void RemovePlayerFromOnlineList(string playerId)
    {
        var deviceId = FindOnlinePlayerDeviceId(playerId);
        if (!string.IsNullOrEmpty(deviceId))
        {
            _onlinePlayers.Remove(deviceId);
            _logger.LogInformation("Player {PlayerId} disconnected", playerId);
        }
    }

    private async Task ProcessMessageAsync(WebSocket socket,
        PlayerState state, Message message)
    {
        try
        {
            if (!_routes.TryGetValue(message.Type, out var handler))
            {
                await socket.PublishAsync(
                    new ErrorMessage("Unknown message type.", 400));
                return;
            }

            await handler(new AppContext
            {
                Message = message,
                PlayerState = state,
                Client = socket,
                OnlinePlayers = _onlinePlayers,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message");
            await socket.PublishAsync(
                new ErrorMessage("Failed to process message.", 500));
        }
    }
}