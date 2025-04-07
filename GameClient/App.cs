using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared;
using Shared.Models;

namespace GameClient;

public class App
{
    private readonly ClientWebSocket _ws;
    private readonly ILogger<App> _logger;
    private readonly ClientSettings _settings;
    private bool _isAuthenticated;
    private Balance _balance;

    public App(ClientWebSocket ws, ILogger<App> logger,
        IOptions<ClientSettings> options)
    {
        _ws = ws;
        _logger = logger;
        _settings = options.Value;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await ConnectAsync(cancellationToken);
        try
        {
            await AuthenticateAsync(cancellationToken);
            await Task.WhenAll(
                ReceiveMessagesAsync(cancellationToken),
                HandleUserInputAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Disconnecting, Goodbye!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred");
        }
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_isAuthenticated)
        {
            var deviceId = PromptDeviceId();
            await _ws.PublishAsync(
                new LoginMessage(deviceId), cancellationToken);
            var msg = await RecieveNextMessageAsync(cancellationToken);
            if (msg is LoginSuccessMessage)
            {
                _isAuthenticated = true;
                var loginMsg = (LoginSuccessMessage)msg;
                _balance = loginMsg.Balance;
                Console.WriteLine(
                    $"Login successful! your player ID is {loginMsg.PlayerId}, share it with your friends and send them gifts!");
                Console.WriteLine($"Your balance is {loginMsg.Balance}");
            }
            else if (msg is ErrorMessage)
            {
                _logger.LogError((msg as ErrorMessage).Message);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var uri = new Uri(_settings.ServerDomain);
        await _ws.ConnectAsync(uri, cancellationToken);
        _logger.LogInformation("Connected to {Server}", _settings.ServerDomain);
    }

    private string PromptDeviceId()
    {
        string deviceId;
        do
        {
            Console.Write("Enter your device ID: ");
            deviceId = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                Console.WriteLine("Invalid input. Please try again.");
            }
        } while (string.IsNullOrWhiteSpace(deviceId));

        return deviceId;
    }

    private async Task HandleUserInputAsync(CancellationToken cancellationToken)
    {
        while (_ws.State == WebSocketState.Open &&
               !cancellationToken.IsCancellationRequested)
        {
            if (!_isAuthenticated)
            {
                await Task.Delay(500, cancellationToken);
                continue;
            }

            Console.WriteLine(
                "Enter the player ID to send a gift (or 'q' to quit):");
            var input = Console.ReadLine();

            if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
            {
                await CloseConnectionAsync();
                break;
            }

            if (Guid.TryParse(input, out var targetId))
            {
                await SendGiftAsync(targetId, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(0.5),
                    cancellationToken);
                await UpdateBalanceAsync(cancellationToken);
            }
            else
            {
                Console.WriteLine(
                    "Invalid input. Provide a valid GUID or 'q' to quit.");
            }
        }
    }

    private async Task SendGiftAsync(Guid targetId,
        CancellationToken cancellationToken)
    {
        var message =
            new SendGiftMessage(targetId.ToString(), ResourceType.Coins, 10);

        await _ws.PublishAsync(message, cancellationToken);
    }

    private async Task CloseConnectionAsync()
    {
        Console.WriteLine("Closing connection...");
        if (_ws.State == WebSocketState.Open ||
            _ws.State == WebSocketState.CloseReceived)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                "Client quitting", CancellationToken.None);
        }

        Console.WriteLine("Press Ctrl+C to exit.");
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        while (_ws.State == WebSocketState.Open &&
               !cancellationToken.IsCancellationRequested)
        {
            var msg = await RecieveNextMessageAsync(cancellationToken);
            if (msg is not null)
            {
                HandleServerMessage(msg);
            }
        }
    }

    private async Task<Message?> RecieveNextMessageAsync(
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        WebSocketReceiveResult result = null;

        try
        {
            using var timeoutCts =
                new CancellationTokenSource(TimeSpan.FromSeconds(100));
            using var linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    timeoutCts.Token, cancellationToken);

            result = await _ws.ReceiveAsync(buffer, linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReceiveAsync failed");
            return null;
        }

        if (result.MessageType == WebSocketMessageType.Close)
        {
            Console.WriteLine("Server initiated close.");
            return null;
        }

        return Message.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
    }

    private void HandleServerMessage(Message msg)
    {
        _logger.LogInformation("Message received from server: {Message}", msg);

        if (msg is GiftAckMessage)
        {
            var ack = (GiftAckMessage)msg;
            var status = ack.Success ? "Successfully" : "Failed";
            Console.WriteLine(
                $"Gift sent {status}, your balance is {ack.Balance}");
        }
        else if (msg is GiftNotificationMessage)
        {
            var gift = (GiftNotificationMessage)msg;
            Console.WriteLine($"You received a gift from {gift.From}!");
            Console.WriteLine($"{gift.ResourceValue} {gift.ResourceType}");
            Console.WriteLine($"Your new balance is {gift.Balance}");
        }
        else if (msg is ErrorMessage)
        {
            var error = (ErrorMessage)msg;
            _logger.LogError(error.Message);
        }
        else if (msg is UpdateResourcesResponseMessage)
        {
            var response = (UpdateResourcesResponseMessage)msg;
            _balance = response.Balance;
            Console.WriteLine(
                $"Your balance has been updated: {response.Balance}");
        }
    }


    private async Task UpdateBalanceAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Updating balance randomly...");
        var resourceTypes = Random.Shared.GetItems(
            Enum.GetValues<ResourceType>().ToArray(),
            1)[0];
        var resourceValue = Random.Shared.Next(1, 100);
        var message =
            new UpdateResourcesMessage(resourceTypes, resourceValue);

        await _ws.PublishAsync(message, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(0.5),
            cancellationToken);
    }
}