namespace Shared.Models;

public readonly record struct Balance(int Coins, int Rolls);

public record PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public int Coins { get; set; }
    public int Rolls { get; set; }

    public Balance Balance => new Balance(Coins, Rolls);
}

public static class PlayerStateExtensions
{
    public static void UpdateResource(this PlayerState state,
        ResourceType resourceType, int value)
    {
        if (resourceType == ResourceType.Coins)
        {
            state.Coins += value;
        }
        else if (resourceType == ResourceType.Rolls)
        {
            state.Rolls += value;
        }
    }

    public static void Update(this PlayerState state, int coins, int rolls)
    {
        state.Coins = coins;
        state.Rolls = rolls;
    }
}