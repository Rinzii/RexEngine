using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Rex.Client.Logging;
using Rex.Shared.Net;

namespace Rex.Client;

/// <summary>Parses command-line args and runs <see cref="ClientApp"/>. May start a listen-server child process.</summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();

            // Allow an environment variable to override the default log level so
            // dev and test runs can be made more or less verbose without recompiling.
            if (Environment.GetEnvironmentVariable("REX_CLIENT_LOG_LEVEL") is { } logLevelStr &&
                Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
            {
                builder.SetMinimumLevel(logLevel);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            }
        });

        var logger = loggerFactory.CreateLogger("Rex.Client");

        if (!CommandLineArgs.TryParse(args, out var parsed, out var parseError))
        {
            logger.CliParseFailed(parseError);
            return;
        }

        foreach (var arg in parsed.UnrecognizedArguments)
        {
            logger.UnrecognizedCliArgument(arg);
        }

        // Listen-server mode means this client process first launches a local server
        // child process, then connects to it as a normal client.
        if (parsed.Mode == NetMode.ListenServer)
        {
            RunWithListenServer(parsed, loggerFactory, logger);
        }
        else
        {
            RunApp(parsed, loggerFactory, logger);
        }
    }

    private static void RunApp(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        using var app = new ClientApp(args.Mode, loggerFactory)
        {
            Headless = args.Headless
        };

        // This token is passed into the app run loop so Ctrl+C can request a clean shutdown.
        using var cts = new CancellationTokenSource();

        // This helper owns the Console.CancelKeyPress subscription.
        // Keeping the event hookup inside a disposable object makes the subscription lifetime
        // explicit and avoids event handlers outliving the objects they use.
        using var shutdownHook = new ShutdownHook(logger, cts, app);

        string? host = null;
        var port = args.Port;

        if (args.ConnectAddress != null)
        {
            // The connect argument may contain just a host or a host plus port.
            // This parser resolves the final host and port pair that the client should use.
            if (!ConnectEndpointParser.TryParse(args.ConnectAddress, args.Port, out var parsedHost, out var parsedPort))
            {
                logger.InvalidConnectAddress(args.ConnectAddress);
                return;
            }

            host = parsedHost;
            port = parsedPort;
        }

        app.Run(host, port, cts.Token);
    }

    private static void RunWithListenServer(CommandLineArgs args, ILoggerFactory loggerFactory, ILogger logger)
    {
        // The bridge keeps the child process alive, keeps logging subscriptions attached,
        // and exposes a wait point for the server ready signal.
        using var serverProcessBridge = StartListenServerProcess(args.Port, logger);
        if (serverProcessBridge == null)
        {
            return;
        }

        try
        {
            // Once the local server is up, we launch the client side in normal client mode
            // and point it at localhost on the requested port.
            var clientArgs = new CommandLineArgs(
                args.Headless,
                NetMode.Client,
                "127.0.0.1",
                args.Port,
                []);

            RunApp(clientArgs, loggerFactory, logger);
        }
        finally
        {
            // Always stop the child server when the client side exits, even if the client run throws.
            StopListenServerProcess(serverProcessBridge.Process, logger);
        }
    }

    private static ListenServerProcessBridge? StartListenServerProcess(int port, ILogger logger)
    {
        var serverAssemblyPath = ResolveServerAssemblyPath();
        if (serverAssemblyPath == null)
        {
            logger.ListenServerAssemblyNotFound();
            return null;
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",

                // We want direct control over stdio so we can watch server output for logs
                // and for the specific ready line that signals startup completion.
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,

                // This is a background helper process, not something that should spawn its own console window.
                CreateNoWindow = true,

                // Set the working directory to the server assembly directory so any relative paths
                // used by the server resolve from its own output location.
                WorkingDirectory = Path.GetDirectoryName(serverAssemblyPath)!
            },

            // Required for some process event scenarios and generally appropriate
            // when this Process instance is being used as a managed runtime wrapper.
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add(serverAssemblyPath);
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add(port.ToString());

        ListenServerProcessBridge? bridge = null;

        try
        {
            // Create the bridge before starting the process so output handlers are already attached
            // when the child begins writing startup logs.
            bridge = new ListenServerProcessBridge(process, logger);

            if (!process.Start())
            {
                logger.ListenServerStartFailed();
                bridge.Dispose();
                return null;
            }

            // Begin async consumption of stdout and stderr.
            // Without this, redirected output can fill buffers and potentially block the child process.
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // The server is expected to print a specific ready line once startup is complete.
            // We wait up to 10 seconds before treating startup as failed.
            if (bridge.WaitUntilReady(TimeSpan.FromSeconds(10)))
            {
                return bridge;
            }

            if (process.HasExited)
            {
                logger.ListenServerExitedEarly();
            }
            else
            {
                logger.ListenServerStartupTimeout();
            }

            StopListenServerProcess(process, logger);
            bridge.Dispose();
            return null;
        }
        catch
        {
            // Dispose the bridge if it exists so event handlers are detached and the process wrapper
            // is cleaned up before the exception continues upward.
            bridge?.Dispose();
            process.Dispose();
            throw;
        }
    }

    private static void StopListenServerProcess(Process process, ILogger logger)
    {
        if (process.HasExited)
        {
            return;
        }

        logger.StoppingListenServer();

        // Kill the full process tree in case the child server spawned children of its own.
        process.Kill(true);

        // Give the OS a short window to report final termination and release resources.
        process.WaitForExit(5000);
    }

    private static string? ResolveServerAssemblyPath()
    {
        var siblingPath = Path.Combine(AppContext.BaseDirectory, "Rex.Server.dll");
        if (File.Exists(siblingPath))
        {
            return siblingPath;
        }

        var outputDir =
            new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var tfm = outputDir.Name;
        var config = outputDir.Parent?.Name;

        // In normal deployed layouts the server DLL may sit next to the client DLL.
        // In local dev layouts it may instead be in its own project output directory, so
        // this walks back from bin/{Config}/{Tfm}/ to the repo root and reconstructs the expected path.
        // TODO: IanP: This might change later as there is a high likely hood we will later change the output location of builds.
        var repoRoot = outputDir.Parent?.Parent?.Parent?.Parent;

        if (config == null || repoRoot == null)
        {
            return null;
        }

        var repoPath = Path.Combine(repoRoot.FullName, "Rex.Server", "bin", config, tfm, "Rex.Server.dll");
        return File.Exists(repoPath) ? repoPath : null;
    }

    private sealed class ShutdownHook : IDisposable
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts;
        private readonly ClientApp _app;
        private bool _disposed;

        public ShutdownHook(ILogger logger, CancellationTokenSource cts, ClientApp app)
        {
            _logger = logger;
            _cts = cts;
            _app = app;

            // Attach once during construction so this helper fully owns the subscription.
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Detach the handler before the captured objects go out of scope.
            // This keeps the event subscription lifetime aligned with the object lifetime.
            Console.CancelKeyPress -= OnCancelKeyPress;
            _disposed = true;
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Mark the signal as handled so the runtime does not immediately tear down the process.
            // We want the app to shut down through its own controlled path instead.
            e.Cancel = true;
            _logger.ShutdownSignalReceived();

            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            _app.Stop();
        }
    }

    private sealed class ListenServerProcessBridge : IDisposable
    {
        private readonly ILogger _logger;
        private readonly ManualResetEventSlim _readySignal;
        private bool _disposed;

        public ListenServerProcessBridge(Process process, ILogger logger)
        {
            Process = process;
            _logger = logger;

            // This is used only to block startup until the server announces readiness.
            // Output logging continues after that for the full process lifetime.
            _readySignal = new ManualResetEventSlim();

            // These handlers stay attached for the full bridge lifetime so we keep receiving
            // stdout and stderr from the child process until shutdown.
            Process.OutputDataReceived += OnOutputDataReceived;
            Process.ErrorDataReceived += OnErrorDataReceived;
        }

        public Process Process { get; }

        public bool WaitUntilReady(TimeSpan timeout)
        {
            return _readySignal.Wait(timeout);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Remove event subscriptions before disposing owned resources so no callback can fire
            // into an object that is already being torn down.
            Process.OutputDataReceived -= OnOutputDataReceived;
            Process.ErrorDataReceived -= OnErrorDataReceived;
            _readySignal.Dispose();
            Process.Dispose();
            _disposed = true;
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            _logger.ListenServerOutput(e.Data);

            // The server writes a sentinel line once startup is complete.
            // Seeing that line releases the startup wait in StartListenServerProcess.
            if (e.Data.Contains(ProtocolConstants.ListenProcessReadyLine, StringComparison.Ordinal))
            {
                _readySignal.Set();
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.ListenServerError(e.Data);
            }
        }
    }
}
