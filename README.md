# Game Server

### .NET 8 WebSocket game server

#### Overview

In order to run the game:
1. Build the solution
    ```
   dotnet build GameServer.sln -c Release
   ```
2. Run the server
    ```
    dotnet run --project GameServer/GameServer.csproj
     ```
3. Run the client 
    ```
    dotnet run --project GameClient/GameClient.csproj
    ```
4. For Running tests
    ```
    dotnet test --no-build --verbosity normal
    ```

#### Configuration
You can configure the server and the client by modifying the `appsettings.json` default configuration file in each project folder:
```json
{
  "ServerSettings": {
    "Host": "127.0.0.1",
    "Port": 5000
  },
  "DbSettings": {
    "ConnectionString": "Data Source=game.db;Pooling=True;",
  }
}
```


If you want to add routes to the server, you can do so by adding a new class that implements the `IRoute` interface.
for example:
```csharp
public class MyRoute : IRoute
{
   public string Event => nameof(MyRoute);
   
   public Func<AppContext, Task> Handler => async (context) =>
    {
        // Handle the request here
        context.Client.PublishAsync(new Message
        {
            Data = new { Message = "Hello, World!" }
        });
    };
}
```
Then, you should register the route to the DI container in the `ConfigureServices` method in `Program.cs`:
```csharp
builder.ConfigureServices((context, services) =>
{
   // All other services
   
   
    // Register your route
    services.AddTransient<IRoute, MyRoute>();
});
```
