using Shared.Models;
using AppContext = GameServer.Models.AppContext;

namespace GameServer.Routes;

public interface IRoute
{
    public string Event { get; }

    public Func<AppContext, Task> Handler { get; }
}