using Microsoft.Extensions.Logging;
using Rex.Server.Core;
using Rex.Shared.Timing;

namespace Rex.Server;

internal static class Program
{
    private static bool _hasStarted;

    internal static void Main(string[] args)
    {
        Start(args);
    }

    private static void Start(string[] args)
    {
        if (_hasStarted)
        {
            throw new InvalidOperationException("Server attempted to start again!");
        }

        _hasStarted = true;

        if (!CommandLineArgs.TryParse(args, out var parsed))
        {
            return;
        }

        ParsedMain(parsed);
    }

    private static void ParsedMain(CommandLineArgs args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("Rex.Server");

        var config = new GameServerConfig
        {
            Port = args.Port,
            MaxPlayers = args.MaxPlayers,
            TickRate = args.TickRate,
            ServerName = "Rex Dedicated Server"
        };

        var server = new GameServer(config, loggerFactory);
        var gameLoop = new GameLoop(config.TickRate)
        {
            YieldBetweenFrames = true
        };

        // Handle graceful shutdown.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received.");
            gameLoop.Stop();
        };

        server.Start();

        gameLoop.OnTick = server.Tick;
        logger.LogInformation("Dedicated server running. Press Ctrl+C to stop.");
        gameLoop.Run();

        server.Shutdown();
    }
}
