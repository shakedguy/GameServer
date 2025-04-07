using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GameServer.Data;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;

namespace GameServer;

public class ClientHandler
{
    private const int MESSAGE_SIZE = 4096;

    private readonly ILogger<ClientHandler> _logger;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<PlayerState> _playerStateRepository;


    private static readonly
        IDictionary<string, (string playerId, WebSocket socket)>
        _onlinePlayers = new Dictionary<string, (string, WebSocket)>();

    private readonly IDictionary<string,
            Func<WebSocket, PlayerState, Message, Task<Message>>>
        _actions;

    public ClientHandler(
        ILogger<ClientHandler> logger,
        IRepository<Player> playerRepository,
        IRepository<PlayerState> playerStateRepository)
    {
        _logger = logger;
        _playerRepository = playerRepository;
        _playerStateRepository = playerStateRepository;

        _actions =
            new Dictionary<string,
                Func<WebSocket, PlayerState, Message, Task<Message>>>
            {
                { nameof(LoginMessage), HandleLoginAsync },
                { nameof(SendGiftMessage), HandleSendGiftAsync },
                {nameof(UpdateResourcesMessage), HandleUpdateResourcesAsync}
            };
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
                    var response =
                        await ProcessMessageAsync(socket, state, message);
                    _logger.LogInformation(
                        "Sending to player {PlayerId} response: {Response} for message {Message}",
                        state.PlayerId, response,
                        JsonSerializer.Deserialize<object>(json));

                    await SendAsync(socket, response);
                }
                else if (!string.IsNullOrEmpty(json))
                {
                    _logger.LogWarning("Unknow message {message}", json);
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

    private async Task<Message> ProcessMessageAsync(WebSocket socket,
        PlayerState state, Message message)
    {
        try
        {
            if (!_actions.TryGetValue(message.Type, out var handler))
            {
                return new ErrorMessage("Unknown message type.", 400);
            }

            return await handler(socket, state, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message");
            return new ErrorMessage("Invalid message format.", 500);
        }
    }

    private async Task<Message> HandleLoginAsync(WebSocket socket,
        PlayerState state, Message message)
    {
        var login = message as LoginMessage;

        if (_onlinePlayers.ContainsKey(login.DeviceId))
        {
            state.PlayerId = _onlinePlayers[login.DeviceId].playerId;
            return new ErrorMessage("Already connected.", 400);
        }

        var player =
            await _playerRepository.GetByAsync(nameof(Player.DeviceId),
                login.DeviceId);
        bool isNewPlayer = player is null;

        PlayerState? newState = null;
        if (isNewPlayer)
        {
            player = await _playerRepository.AddAsync(new Player
                { DeviceId = login.DeviceId });
            if (player == null)
                return new ErrorMessage("Failed to create player.", 500);
            newState = await CreateNewStateAsync(player.Id);
        }
        else
        {
            newState = await _playerStateRepository.GetAsync(player.Id);
        }

        if (newState is null)
        {
            return new ErrorMessage("Player not found.", 400);
        }

        state.PlayerId = player.Id;
        state.Update(newState.Coins, newState.Rolls);

        _onlinePlayers[login.DeviceId] = (player.Id, socket);
        return new LoginSuccessMessage(player.Id, state.Balance);
    }

    private Task<PlayerState?> CreateNewStateAsync(string playerId)
    {
        return _playerStateRepository.AddAsync(new PlayerState
        {
            PlayerId = playerId,
            Coins = 100,
            Rolls = 50
        });
    }

    private async Task<Message> HandleSendGiftAsync(WebSocket socket,
        PlayerState senderState, Message message)
    {
        var gift = message as SendGiftMessage;
        var receiverState = await _playerStateRepository.GetAsync(gift.To);

        if (receiverState is null)
        {
            _logger.LogWarning(
                "Player {SenderId} tried to send gift to unknown player {ReceiverId}",
                senderState.PlayerId, gift.To);
            return new ErrorMessage("Player not found.", 404);
        }

        UpdateStates(senderState, receiverState, gift);

        await Task.WhenAll(
            _playerStateRepository.UpdateAsync(senderState),
            _playerStateRepository.UpdateAsync(receiverState)
        );

        await NotifyGiftReceiverAsync(senderState, gift, receiverState);

        return new GiftAckMessage(true, senderState.Balance);
    }


    private async Task<Message> HandleUpdateResourcesAsync(WebSocket socket,
        PlayerState state, Message message)
    {
        var updateMessage = (UpdateResourcesMessage)message;
        state.UpdateResource(updateMessage.ResourceType, updateMessage.ResourceValue);
        await _playerStateRepository.UpdateAsync(state);
        return new UpdateResourcesResponseMessage(state.Balance);
    }
    
    
    private void UpdateStates(PlayerState sender, PlayerState receiver,
        SendGiftMessage gift)
    {
        var value = Math.Abs(gift.ResourceValue);
        receiver.UpdateResource(gift.ResourceType, value);
        sender.UpdateResource(gift.ResourceType, -value);
    }

    private async Task NotifyGiftReceiverAsync(PlayerState sender,
        SendGiftMessage gift, PlayerState receiverState)
    {
        var receiverDeviceId = FindOnlinePlayerDeviceId(gift.To);
        if (receiverDeviceId is null ||
            !_onlinePlayers.TryGetValue(receiverDeviceId,
                out var receiverInfo)) return;

        _logger.LogInformation("Sending gift notification to {PlayerId}",
            receiverState.PlayerId);
        var notification = new GiftNotificationMessage(sender.PlayerId,
            gift.ResourceType, gift.ResourceValue, receiverState.Balance);
        await SendAsync(receiverInfo.socket, notification);
    }

    private async Task SendAsync(WebSocket socket, Message message)
    {
        try
        {
            await socket.PublishAsync(message);
            _logger.LogInformation("Message {MessageId} sent successfully",
                message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
        }
    }
    
    
    
}