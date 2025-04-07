using System.Net.WebSockets;
using System.Reflection;
using GameServer;
using GameServer.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Models;

namespace Tests;

public class ClientHandlerTests
{
    private readonly Mock<IRepository<Player>> _playerRepoMock = new();
    private readonly Mock<IRepository<PlayerState>> _stateRepoMock = new();
    private readonly Mock<ILogger<ClientHandler>> _loggerMock = new();
    private readonly ClientHandler _handler;

    public ClientHandlerTests()
    {
        _handler = new ClientHandler(_loggerMock.Object, _playerRepoMock.Object,
            _stateRepoMock.Object);
    }

    [Fact]
    public async Task
        HandleLoginAsync_Should_Return_LoginSuccessMessage_For_New_Player()
    {
        var socket = new Mock<WebSocket>();
        var loginMsg = new LoginMessage("device-123");
        var player = new Player { Id = Guid.NewGuid().ToString(), DeviceId = "device-123" };
        var newState = new PlayerState
            { PlayerId = player.Id, Coins = 100, Rolls = 50 };

        _playerRepoMock.Setup(r =>
                r.GetByAsync(nameof(Player.DeviceId), "device-123"))
            .ReturnsAsync((Player)null);
        _playerRepoMock.Setup(r => r.AddAsync(It.IsAny<Player>()))
            .ReturnsAsync(player);
        _stateRepoMock.Setup(r => r.AddAsync(It.IsAny<PlayerState>()))
            .ReturnsAsync(newState);

        var dummyState = new PlayerState();


        var result = await _handler
            .GetType()
            .GetMethod("HandleLoginAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .InvokeAsync<Message>(_handler, socket.Object, dummyState,
                loginMsg);


        Assert.IsType<LoginSuccessMessage>(result);
        var loginSuccess = (LoginSuccessMessage)result;
        Assert.Equal(player.Id, loginSuccess.PlayerId);
        Assert.Equal(newState.Balance.Coins, loginSuccess.Balance.Coins);
    }

    [Fact]
    public async Task
        ProcessMessageAsync_Should_Return_ErrorMessage_On_Unknown_Type()
    {
        var message = new Mock<Message>();
        message.Setup(m => m.Type).Returns("InvalidType");

        var socket = new Mock<WebSocket>();
        var state = new PlayerState();

        var result = await _handler
            .GetType()
            .GetMethod("ProcessMessageAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .InvokeAsync<Message>(_handler, socket.Object, state,
                message.Object);

        Assert.IsType<ErrorMessage>(result);
        Assert.Equal("Unknown message type.", (result as ErrorMessage).Message);
    }

    [Fact]
    public async Task
        HandleSendGiftAsync_Should_Return_Error_When_Receiver_Not_Found()
    {
        var senderState = new PlayerState
            { PlayerId = Guid.NewGuid().ToString(), Coins = 50 };
        var gift = new SendGiftMessage("receiver-999", ResourceType.Coins, 10);

        _stateRepoMock.Setup(repo => repo.GetAsync("receiver-999"))
            .ReturnsAsync((PlayerState)null);

        var socket = new Mock<WebSocket>();
        var result = await _handler
            .GetType()
            .GetMethod("HandleSendGiftAsync",
                BindingFlags.NonPublic | BindingFlags.Instance)
            .InvokeAsync<Message>(_handler, socket.Object, senderState, gift);

        Assert.IsType<ErrorMessage>(result);
        Assert.Equal("Player not found.", (result as ErrorMessage).Message);
    }
}