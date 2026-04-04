using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Shared.Net;
using Rex.Shared.Logging;
using Rex.Shared.Net;

namespace Rex.Sandbox.Client;

/// <summary>
/// Sandbox CLI entrypoint. This remains in-repo but models how a future game-side consumer would boot the engine.
/// </summary>
internal static class Program
{
    private const string ServerAssemblyEnvironmentVariable = "REX_SANDBOX_SERVER_DLL";

    private static void Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();

            if (Environment.GetEnvironmentVariable("REX_SANDBOX_CLIENT_LOG_LEVEL") is { } logLevelStr &&
                Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
            {
                builder.SetMinimumLevel(logLevel);
            }
            else
            {
                builder.SetMinimumLevel(LogLevel.Warning);
            }
        });

        var logger = loggerFactory.CreateLogger("Rex.Sandbox.Client");

        if (!CommandLineArgs.TryParse(args, out var parsed, out var parseError))
        {
            logger.CliParseFailed(parseError);
            return;
        }

        foreach (var arg in parsed.UnrecognizedArguments)
        {
            logger.UnrecognizedCliArgument(arg);
        }

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

        using var cts = new CancellationTokenSource();
        using var shutdownHook = new ShutdownHook(logger, cts, app);

        string? host = null;
        var port = args.Port;

        if (args.ConnectAddress != null)
        {
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
        using var serverProcessBridge = StartListenServerProcess(args.Port, logger);
        if (serverProcessBridge == null)
        {
            return;
        }

        try
        {
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
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(serverAssemblyPath)!
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.ArgumentList.Add(serverAssemblyPath);
        process.StartInfo.ArgumentList.Add("--port");
        process.StartInfo.ArgumentList.Add(port.ToString());

        ListenServerProcessBridge? bridge = null;

        try
        {
            bridge = new ListenServerProcessBridge(process, logger);

            if (!process.Start())
            {
                logger.ListenServerStartFailed();
                bridge.Dispose();
                return null;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

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
        process.Kill(true);
        process.WaitForExit(5000);
    }

    private static string? ResolveServerAssemblyPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(ServerAssemblyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath) ? configuredPath : null;
        }

        var siblingPath = Path.Combine(AppContext.BaseDirectory, "Rex.Sandbox.Server.dll");
        if (File.Exists(siblingPath))
        {
            return siblingPath;
        }

        var outputDir =
            new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
        var tfm = outputDir.Name;
        var config = outputDir.Parent?.Name;
        var repoRoot = outputDir.Parent?.Parent?.Parent?.Parent;

        if (config == null || repoRoot == null)
        {
            return null;
        }

        var repoPath = Path.Combine(repoRoot.FullName, "Rex.Sandbox.Server", "bin", config, tfm,
            "Rex.Sandbox.Server.dll");
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
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Console.CancelKeyPress -= OnCancelKeyPress;
            _disposed = true;
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
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
            _readySignal = new ManualResetEventSlim();
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

            if (e.Data.Contains(SandboxProtocolConstants.ListenProcessReadyLine, StringComparison.Ordinal))
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

internal sealed class CommandLineArgs
{
    public bool Headless { get; }
    public NetMode Mode { get; }
    public string? ConnectAddress { get; }
    public int Port { get; }
    public IReadOnlyList<string> UnrecognizedArguments { get; }

    internal CommandLineArgs(
        bool headless,
        NetMode mode,
        string? connectAddress,
        int port,
        IReadOnlyList<string> unrecognizedArguments)
    {
        Headless = headless;
        Mode = mode;
        ConnectAddress = connectAddress;
        Port = port;
        UnrecognizedArguments = unrecognizedArguments;
    }

    public static bool TryParse(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out CommandLineArgs? parsed,
        [NotNullWhen(false)] out string? error)
    {
        parsed = null;
        error = null;
        var headless = false;
        var listenServer = false;
        var standalone = false;
        string? connectAddress = null;
        var port = ProtocolConstants.DefaultPort;
        var unrecognized = new List<string>();

        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            switch (arg)
            {
                case "--headless":
                    headless = true;
                    break;
                case "--listen":
                    listenServer = true;
                    break;
                case "--standalone":
                    standalone = true;
                    break;
                case "--connect" when !enumerator.MoveNext():
                    error = "Missing value for --connect.";
                    return false;
                case "--connect":
                    connectAddress = enumerator.Current;
                    break;
                case "--port" when !enumerator.MoveNext():
                    error = "Missing value for --port.";
                    return false;
                case "--port":
                    if (!int.TryParse(enumerator.Current, out port))
                    {
                        error = "Invalid value for --port.";
                        return false;
                    }

                    break;
                default:
                    unrecognized.Add(arg);
                    break;
            }
        }

        NetMode mode;
        if (standalone)
        {
            mode = NetMode.Standalone;
        }
        else if (connectAddress != null)
        {
            mode = NetMode.Client;
        }
        else if (listenServer)
        {
            mode = NetMode.ListenServer;
        }
        else
        {
            mode = NetMode.Standalone;
        }

        parsed = new CommandLineArgs(headless, mode, connectAddress, port, unrecognized);
        return true;
    }
}

internal static partial class ClientProgramLog
{
    [LoggerMessage(EventId = LogEventIds.ClientHost.ShutdownSignal, Level = LogLevel.Information,
        Message = "Shutdown signal received.")]
    public static partial void ShutdownSignalReceived(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStdout, Level = LogLevel.Information,
        Message = "[Server] {Message}")]
    public static partial void ListenServerOutput(this ILogger logger, string message);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStderr, Level = LogLevel.Error,
        Message = "[Server] {Message}")]
    public static partial void ListenServerError(this ILogger logger, string message);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerAssemblyNotFound, Level = LogLevel.Error,
        Message =
            "Could not find Rex.Sandbox.Server assembly for listen server mode. Set REX_SANDBOX_SERVER_DLL to override the default lookup.")]
    public static partial void ListenServerAssemblyNotFound(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStartFailed, Level = LogLevel.Error,
        Message = "Failed to start Rex.Sandbox.Server process.")]
    public static partial void ListenServerStartFailed(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerExitedEarly, Level = LogLevel.Error,
        Message = "Listen server exited before startup completed.")]
    public static partial void ListenServerExitedEarly(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.ListenServerStartupTimeout, Level = LogLevel.Error,
        Message = "Timed out waiting for listen server startup.")]
    public static partial void ListenServerStartupTimeout(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.StoppingListenServer, Level = LogLevel.Information,
        Message = "Stopping listen server process.")]
    public static partial void StoppingListenServer(this ILogger logger);

    [LoggerMessage(EventId = LogEventIds.ClientHost.InvalidConnectAddress, Level = LogLevel.Error,
        Message =
            "Invalid connect address \"{ConnectAddress}\". Use host, host:port, or bracketed IPv6 such as [::1]:port.")]
    public static partial void InvalidConnectAddress(this ILogger logger, string connectAddress);

    [LoggerMessage(EventId = LogEventIds.ClientHost.CliParseFailed, Level = LogLevel.Error,
        Message = "Command-line parse failed: {Reason}")]
    public static partial void CliParseFailed(this ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ClientHost.UnrecognizedCliArgument, Level = LogLevel.Warning,
        Message = "Ignoring unrecognized command-line argument: {Argument}")]
    public static partial void UnrecognizedCliArgument(this ILogger logger, string argument);
}
