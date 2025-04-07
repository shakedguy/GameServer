using System.Net.WebSockets;
using Shared.Models;

namespace GameServer.Models;

public class AppContext
{
    public IDictionary<string, (string PlayerId, WebSocket Socket)> OnlinePlayers { get; set; }

    public WebSocket Client { get; set; }

    public PlayerState PlayerState { get; set; }

    public Message Message { get; set; }
}