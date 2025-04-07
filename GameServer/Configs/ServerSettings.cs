namespace GameServer.Configs;

public class ServerSettings
{
    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5000;

    public string Url => $"http://{Host}:{Port}/ws/";
}