using Shared.Models;

namespace Tests;

public class PlayerStateExtensionsTests
{
    [Theory]
    [InlineData(ResourceType.Coins, 10, 10, 0)]
    [InlineData(ResourceType.Coins, -5, -5, 0)]
    [InlineData(ResourceType.Rolls, 7, 0, 7)]
    [InlineData(ResourceType.Rolls, -3, 0, -3)]
    public void UpdateResource_Should_Modify_Resource_Correctly(ResourceType type, int value, int expectedCoins, int expectedRolls)
    {
        var player = new PlayerState();

        player.UpdateResource(type, value);
        
        Assert.Equal(expectedCoins, player.Coins);
        Assert.Equal(expectedRolls, player.Rolls);
    }
    
    [Theory]
    [InlineData(50, 20)]
    [InlineData(0, 0)]
    [InlineData(-10, -5)]
    public void Update_Should_Set_Both_Coins_And_Rolls(int coins, int rolls)
    {
        var player = new PlayerState();

        player.Update(coins, rolls);

        Assert.Equal(coins, player.Coins);
        Assert.Equal(rolls, player.Rolls);
    }
}