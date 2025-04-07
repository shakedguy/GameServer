using System.Net.WebSockets;
using GameServer.Data;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using AppContext = GameServer.Models.AppContext;

namespace GameServer.Routes;

public class LoginRoute : IRoute
{
    private readonly ILogger<LoginRoute> _logger;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<PlayerState> _playerStateRepository;
    public string Event => nameof(LoginMessage);
    public Func<AppContext, Task> Handler { get; set; }

    public LoginRoute(
        ILogger<LoginRoute> logger,
        IRepository<Player> playerRepository,
        IRepository<PlayerState> playerStateRepository
    )
    {
        _logger = logger;
        _playerRepository = playerRepository;
        _playerStateRepository = playerStateRepository;
        Handler = HandleMessage;
    }


    private async Task HandleMessage(AppContext context)
    {
        var login = (LoginMessage)context.Message;

        if (context.OnlinePlayers.ContainsKey(login.DeviceId))
        {
            context.PlayerState.PlayerId =
                context.OnlinePlayers[login.DeviceId].PlayerId;
            await context.Client.PublishAsync(
                new ErrorMessage("Already connected.", 400));
            return;
        }

        var player =
            await GetOrCreatePlayer(login.DeviceId);
        if (player is null)
        {
            await context.Client.PublishAsync(
                new ErrorMessage("Failed to create player.", 500));
            return;
        }

        var newState = await GetOrCreateState(player.Id);
        if (newState is null)
        {
            await context.Client.PublishAsync(
                new ErrorMessage("Failed to create player state.", 500));
            return;
        }

        context.PlayerState.PlayerId = player.Id;
        context.PlayerState.Update(newState.Coins, newState.Rolls);
        context.OnlinePlayers[login.DeviceId] = (player.Id, context.Client);
        await context.Client.PublishAsync(
            new LoginSuccessMessage(player.Id, context.PlayerState.Balance));
    }


    private async Task<Player?> GetOrCreatePlayer(string deviceId)
    {
        var player =
            await _playerRepository.GetByAsync(nameof(Player.DeviceId),
                deviceId);
        if (player is null)
        {
            player = await _playerRepository.AddAsync(new Player
                { DeviceId = deviceId });
        }

        return player;
    }

    private async Task<PlayerState?> GetOrCreateState(string playerId)
    {
        var state = await _playerStateRepository.GetAsync(playerId);
        if (state is null)
        {
            state = await CreateNewStateAsync(playerId);
        }

        return state;
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
}