namespace GameServer.Configs;

public class DbSettings
{
    public string ConnectionString { get; init; } = "Data Source=game.db";

    public string MigrationsPath { get; set; } = "Migrations";
}