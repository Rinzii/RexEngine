using System.Threading;
using Microsoft.Extensions.Logging;
using Rex.Server.Logging;
using Rex.Server.Simulation;

namespace Rex.Server;

/// <summary>Headless dedicated server. Reads CLI args into <see cref="GameServerConfig"/> and runs <see cref="ServerApp"/>.</summary>
internal static class Program
{
    internal static void Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var bootstrapLogger = loggerFactory.CreateLogger("Rex.Server");

        if (!CommandLineArgs.TryParse(args, out var parsed, out var parseError))
        {
            bootstrapLogger.CliParseFailed(parseError ?? "Invalid arguments.");
            return;
        }

        foreach (var arg in parsed.UnrecognizedArguments)
        {
            bootstrapLogger.UnrecognizedCliArgument(arg);
        }

        var config = new GameServerConfig
        {
            Port = parsed.Port,
            MaxPlayers = parsed.MaxPlayers,
            TickRate = parsed.TickRate,
            ServerName = "Rex Dedicated Server"
        };

        using var app = new ServerApp(config, loggerFactory);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            app.Stop();
        };

        try
        {
            app.Run(cts.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in use", StringComparison.Ordinal))
        {
            bootstrapLogger.PortAlreadyInUse(ex.Message);
            Environment.Exit(1);
        }
    }
}
