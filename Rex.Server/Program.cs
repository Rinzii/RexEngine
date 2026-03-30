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
            bootstrapLogger.CliParseFailed(parseError);
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

        // Own the Ctrl+C subscription inside a disposable helper so the event handler
        // is removed before the captured objects go out of scope.
        using var shutdownHook = new ShutdownHook(cts, app);

        try
        {
            // Run until shutdown is requested through the cancellation token or until
            // the server stops for some other reason.
            app.Run(cts.Token);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in use", StringComparison.Ordinal))
        {
            // Surface a clearer startup failure when the requested port is already bound
            // by another process.
            bootstrapLogger.PortAlreadyInUse(ex.Message);
            Environment.Exit(1);
        }
    }

    private sealed class ShutdownHook : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly ServerApp _app;
        private bool _disposed;

        public ShutdownHook(CancellationTokenSource cts, ServerApp app)
        {
            _cts = cts;
            _app = app;

            // Subscribe during construction so this helper fully owns the handler lifetime.
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Always detach the handler before the token source and app are disposed.
            Console.CancelKeyPress -= OnCancelKeyPress;
            _disposed = true;
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Prevent the runtime from immediately terminating the process so the server
            // can stop through its normal shutdown path.
            e.Cancel = true;

            // Cancel only once in case Ctrl+C is pressed multiple times.
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            // Request the app to stop its work and exit its run loop.
            _app.Stop();
        }
    }
}
