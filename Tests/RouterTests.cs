using System.Net.WebSockets;
using System.Reflection;
using GameServer;
using GameServer.Data;
using GameServer.Routes;
using Microsoft.Extensions.Logging;
using Moq;
using Shared;
using Shared.Models;

namespace Tests;

using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppContext = GameServer.Models.AppContext;

public class RouterTests
{
    private readonly Mock<IRepository<Player>> _playerRepo = new();
    private readonly Mock<IRepository<PlayerState>> _stateRepo = new();
    private readonly Mock<ILogger<Router>> _logger = new();

    private readonly Mock<IRoute> _mockRoute;
    private readonly Router _router;

    public RouterTests()
    {
        _mockRoute = new Mock<IRoute>();
        _mockRoute.SetupGet(r => r.Event).Returns(nameof(TestMessage));
        _mockRoute.SetupGet(r => r.Handler).Returns((AppContext context) =>
        {
            context.PlayerState.Coins += 10;
            return Task.CompletedTask;
        });

        var routes = new List<IRoute> { _mockRoute.Object };
        _router = new Router(_logger.Object, _playerRepo.Object,
            _stateRepo.Object, routes);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Invoke_Correct_Route_Handler()
    {
        var socket = new Mock<WebSocket>();
        var state = new PlayerState();
        var message = new TestMessage();


        await InvokePrivateProcessMessage(_router, socket.Object, state,
            message);


        Assert.Equal(10, state.Coins);
    }


    private async Task InvokePrivateProcessMessage(Router router,
        WebSocket socket, PlayerState state, Message message)
    {
        var method = typeof(Router)
            .GetMethod("ProcessMessageAsync",
                BindingFlags.NonPublic |
                BindingFlags.Instance);

        await (Task)method!.Invoke(router,
            new object[] { socket, state, message })!;
    }


    public record TestMessage : Message
    {
        public override string Type => nameof(TestMessage);
    }
}