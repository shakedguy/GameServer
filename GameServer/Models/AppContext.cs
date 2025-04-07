using System.Net.WebSockets;
using Shared.Models;

namespace GameServer.Models;

public class AppContext
{
    public required IDictionary<string, (string PlayerId, WebSocket Socket)>
        OnlinePlayers { get; init; }

    public required WebSocket Client { get; init; }

    public required PlayerState PlayerState { get; init; }

    public required Message Message { get; init; }
}