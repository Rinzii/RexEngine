using Microsoft.Extensions.Logging;
using Rex.Server.Simulation;

namespace Rex.Server;

/// <summary>Headless dedicated server: CLI args, <see cref="GameServerConfig"/>, then <see cref="ServerApp"/>.</summary>
internal static class Program
{
    internal static void Main(string[] args)
    {
        if (!CommandLineArgs.TryParse(args, out var parsed))
            return;

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var config = new GameServerConfig
        {
            Port = parsed.Port,
            MaxPlayers = parsed.MaxPlayers,
            TickRate = parsed.TickRate,
            ServerName = "Rex Dedicated Server"
        };

        using var app = new ServerApp(config, loggerFactory);

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            app.Stop();
        };

        app.Run();
    }
}
