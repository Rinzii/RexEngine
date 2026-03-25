using Microsoft.Extensions.Logging;
using Rex.Client.Net;
using Rex.Shared.Net;
using Rex.Shared.Server;
using Rex.Shared.Timing;

namespace Rex.Client;

internal static class Program
{
    private static void Main(string[] args)
    {
        if (!CommandLineArgs.TryParse(args, out var parsed))
        {
            return;
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (parsed.ListenServer)
        {
            RunListenServer(parsed, loggerFactory, logger);
        }
        else if (parsed.ConnectAddress != null)
        {
            RunRemoteClient(parsed, loggerFactory, logger);
        }
        else
        {
            logger.LogError("No mode specified. Use --listen to host or --connect <ip> to join.");
        }
    }

    private static void RunListenServer(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        var serverConfig = new GameServerConfig
        {
            Port = args.Port,
            TickRate = ProtocolConstants.DefaultTickRate,
            ServerName = "Rex Listen Server"
        };

        var server = new GameServer(serverConfig, loggerFactory);
        server.Start();

        // Create local client channel -- zero latency, no network round-trip for host.
        var localChannel = server.AddLocalClient();

        var client = new GameClient(loggerFactory);
        client.Connect(localChannel);

        var gameLoop = new GameLoop(serverConfig.TickRate)
        {
            YieldBetweenFrames = !ShouldRender(args)
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received.");
            gameLoop.Stop();
        };

        gameLoop.OnTick = () =>
        {
            server.Tick();
            client.Tick(gameLoop.Clock.CurrentTick);
        };

        gameLoop.OnRender = alpha =>
        {
            if (!ShouldRender(args))
                return;

            // Future: render using SDL with interpolated state.
            // var entities = client.WorldState.GetInterpolatedState(alpha);
        };

        logger.LogInformation("Listen server running on port {Port}. Press Ctrl+C to stop.", args.Port);
        gameLoop.Run();

        client.Disconnect();
        server.Shutdown();
    }

    private static void RunRemoteClient(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        var client = new GameClient(loggerFactory);

        // Parse host:port from connect address.
        var address = args.ConnectAddress!;
        var host = address;
        var port = args.Port;

        var colonIndex = address.LastIndexOf(':');
        if (colonIndex > 0 && int.TryParse(address[(colonIndex + 1)..], out var parsedPort))
        {
            host = address[..colonIndex];
            port = parsedPort;
        }

        var gameLoop = new GameLoop(ProtocolConstants.DefaultTickRate)
        {
            YieldBetweenFrames = !ShouldRender(args)
        };

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown signal received.");
            gameLoop.Stop();
        };

        client.Connect(host, port);

        gameLoop.OnTick = () =>
        {
            client.Tick(gameLoop.Clock.CurrentTick);
        };

        gameLoop.OnRender = alpha =>
        {
            if (!ShouldRender(args))
                return;

            // Future: render using SDL with interpolated state.
            // var entities = client.WorldState.GetInterpolatedState(alpha);
        };

        logger.LogInformation("Connecting to {Host}:{Port}. Press Ctrl+C to stop.", host, port);
        gameLoop.Run();

        client.Disconnect();
    }

    private static bool ShouldRender(CommandLineArgs args)
    {
        return !args.Headless;
    }
}
