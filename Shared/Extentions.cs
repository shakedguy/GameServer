using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Shared;

public static class Extentions
{
    public static async Task PublishAsync(this WebSocket socket, object message,
        CancellationToken cancellationToken = default)
    {
        await socket.SendAsync(Serialize(message), WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public static async Task PublishAsync(this ClientWebSocket socket,
        object message,
        CancellationToken cancellationToken = default)
    {
        await socket.SendAsync(Serialize(message), WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private static byte[] Serialize(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return Encoding.UTF8.GetBytes(json);
    }
    
    
}