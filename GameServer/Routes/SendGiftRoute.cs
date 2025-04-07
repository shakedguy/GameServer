using GameServer.Data;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using AppContext = GameServer.Models.AppContext;

namespace GameServer.Routes;

public class SendGiftRoute : IRoute
{
    private readonly ILogger<SendGiftRoute> _logger;
    private readonly IRepository<PlayerState> _playerStateRepository;
    public string Event => nameof(SendGiftMessage);
    public Func<AppContext, Task> Handler { get; }

    public SendGiftRoute(
        ILogger<SendGiftRoute> logger,
        IRepository<PlayerState> playerStateRepository
    )
    {
        _logger = logger;
        _playerStateRepository = playerStateRepository;
        Handler = HandleSendGift;
    }

    private async Task HandleSendGift(AppContext context)
    {
        var gift = context.Message as SendGiftMessage;
        var receiverState = await _playerStateRepository.GetAsync(gift.To);

        if (receiverState is null)
        {
            _logger.LogWarning(
                "Player {SenderId} tried to send gift to unknown player {ReceiverId}",
                context.PlayerState.PlayerId, gift.To);
            await context.Client.PublishAsync(
                new ErrorMessage("Player not found.", 404));
            return;
        }

        UpdateStates(context.PlayerState, receiverState, gift);

        await Task.WhenAll(
            _playerStateRepository.UpdateAsync(context.PlayerState),
            _playerStateRepository.UpdateAsync(receiverState)
        );

        await NotifyGiftReceiverAsync(context, gift, receiverState);

        await context.Client.PublishAsync(
            new GiftAckMessage(true, context.PlayerState.Balance));
    }

    private void UpdateStates(PlayerState sender, PlayerState receiver,
        SendGiftMessage gift)
    {
        var value = Math.Abs(gift.ResourceValue);
        receiver.UpdateResource(gift.ResourceType, value);
        sender.UpdateResource(gift.ResourceType, -value);
    }

    private async Task NotifyGiftReceiverAsync(AppContext context,
        SendGiftMessage gift, PlayerState receiverState)
    {
        var receiverDeviceId = FindOnlinePlayerDeviceId(context, gift.To);
        if (receiverDeviceId is null ||
            !context.OnlinePlayers.TryGetValue(receiverDeviceId,
                out var receiverInfo)) return;

        _logger.LogInformation("Sending gift notification to {PlayerId}",
            receiverState.PlayerId);
        var notification = new GiftNotificationMessage(
            context.PlayerState.PlayerId,
            gift.ResourceType, gift.ResourceValue, receiverState.Balance);
        await receiverInfo.Socket.PublishAsync(notification);
    }

    private string FindOnlinePlayerDeviceId(AppContext context, string playerId)
    {
        var playerEntry =
            context.OnlinePlayers.FirstOrDefault(kv =>
                kv.Value.PlayerId == playerId);

        return playerEntry.Key ?? string.Empty;
    }
}