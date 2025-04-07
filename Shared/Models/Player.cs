using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models;

public record Player
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public required string DeviceId { get; set; }
}