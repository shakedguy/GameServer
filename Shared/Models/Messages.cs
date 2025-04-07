using System.Text.Json;

namespace Shared.Models;

public enum ResourceType
{
    Coins,
    Rolls
}

public abstract record Message
{
    public Guid MessageId { get; } = Guid.NewGuid();
    public abstract string Type { get; }

    public static Message Parse(string json)
    {
        var type = JsonDocument.Parse(json).RootElement.GetProperty("Type")
            .GetString();

        if (string.IsNullOrEmpty(type))
        {
            throw new JsonException("Invalid message type");
        }

        return type! switch
        {
            nameof(LoginMessage) => JsonSerializer.Deserialize<LoginMessage>(
                json),
            nameof(ErrorMessage) => JsonSerializer.Deserialize<ErrorMessage>(
                json),
            nameof(GiftAckMessage) =>
                JsonSerializer.Deserialize<GiftAckMessage>(json),
            nameof(SendGiftMessage) => JsonSerializer
                .Deserialize<SendGiftMessage>(json),
            nameof(GiftNotificationMessage) => JsonSerializer
                .Deserialize<GiftNotificationMessage>(json),
            nameof(LoginSuccessMessage) => JsonSerializer
                .Deserialize<LoginSuccessMessage>(json),
            nameof(UpdateResourcesMessage) => JsonSerializer
                .Deserialize<UpdateResourcesMessage>(json),
            nameof(UpdateResourcesResponseMessage) => JsonSerializer
                .Deserialize<UpdateResourcesResponseMessage>(json),
            _ => throw new JsonException("Invalid message type")
        };
    }

    public static bool TryParse(string json, out Message message)
    {
        try
        {
            message = Parse(json);
            return true;
        }
        catch
        {
            message = null!;
            return false;
        }
    }
}

public record LoginMessage(string DeviceId) : Message
{
    public override string Type => nameof(LoginMessage);
}

public record LoginSuccessMessage(string PlayerId, Balance Balance)
    : Message
{
    public override string Type => nameof(LoginSuccessMessage);
}

public record SendGiftMessage(
    string To,
    ResourceType ResourceType,
    int ResourceValue) : Message
{
    public override string Type => nameof(SendGiftMessage);
}

public record GiftAckMessage(bool Success, Balance Balance) : Message
{
    public override string Type => nameof(GiftAckMessage);
}

public record GiftNotificationMessage(
    string From,
    ResourceType ResourceType,
    int ResourceValue,
    Balance Balance
) : Message
{
    public override string Type => nameof(GiftNotificationMessage);
}

public record ErrorMessage(string Message, int StatusCode)
    : Message
{
    public override string Type => nameof(ErrorMessage);
}

public record UpdateResourcesMessage(
    ResourceType ResourceType,
    int ResourceValue) : Message
{
    public override string Type => nameof(UpdateResourcesMessage);
}

public record UpdateResourcesResponseMessage(Balance Balance) : Message
{
    public override string Type => nameof(UpdateResourcesResponseMessage);
}