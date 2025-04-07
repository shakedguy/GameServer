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
        var login = context.Message as LoginMessage;

        if (context.OnlinePlayers.ContainsKey(login.DeviceId))
        {
            context.PlayerState.PlayerId =
                context.OnlinePlayers[login.DeviceId].PlayerId;
            await context.Client.PublishAsync(
                new ErrorMessage("Already connected.", 400));
            return;
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
            {
                await context.Client.PublishAsync(
                    new ErrorMessage("Failed to create player.", 500));
                return;
            }

            newState = await CreateNewStateAsync(player.Id);
        }
        else
        {
            newState = await _playerStateRepository.GetAsync(player.Id);
        }

        if (newState is null)
        {
            await context.Client.PublishAsync(
                new ErrorMessage("Player not found.", 400));
            return;
        }

        context.PlayerState.PlayerId = player.Id;
        context.PlayerState.Update(newState.Coins, newState.Rolls);

        context.OnlinePlayers[login.DeviceId] = (player.Id, context.Client);
        await context.Client.PublishAsync(
            new LoginSuccessMessage(player.Id, context.PlayerState.Balance));
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