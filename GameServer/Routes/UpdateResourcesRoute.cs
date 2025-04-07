using GameServer.Data;
using Microsoft.Extensions.Logging;
using Shared;
using Shared.Models;
using AppContext = GameServer.Models.AppContext;

namespace GameServer.Routes;

public class UpdateResourcesRoute : IRoute
{
    private readonly ILogger<UpdateResourcesRoute> _logger;
    private readonly IRepository<PlayerState> _playerStateRepository;
    public string Event => nameof(UpdateResourcesMessage);
    public Func<AppContext, Task> Handler { get; }


    public UpdateResourcesRoute(
        ILogger<UpdateResourcesRoute> logger,
        IRepository<PlayerState> playerStateRepository
    )
    {
        _logger = logger;
        _playerStateRepository = playerStateRepository;
        Handler = UpdateResources;
    }

    private async Task UpdateResources(AppContext context)
    {
        try
        {
            var updateMessage = (UpdateResourcesMessage)context.Message;
            context.PlayerState.UpdateResource(updateMessage.ResourceType,
                updateMessage.ResourceValue);
            await _playerStateRepository.UpdateAsync(context.PlayerState);
            await context.Client.PublishAsync(
                new UpdateResourcesResponseMessage(context.PlayerState
                    .Balance));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            await context.Client.PublishAsync(
                new ErrorMessage("Failed to update resources.", 500));
        }
    }
}