using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rex.Client;
using Rex.Sandbox.Shared.Net;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Startup;

namespace Rex.Sandbox.Client;

/// <summary>Sandbox sample CLI entry. Mirrors how a game-hosted client boots the engine.</summary>
internal static class Program
{
    private const string ServerAssemblyEnvironmentVariable = "REX_SANDBOX_SERVER_DLL";

    [STAThread]
    private static void Main(string[] args)
    {
        Start(args);
    }

    private static void Start(string[] args)
    {
        using WindowCreator creator = new WindowCreator("Hi", 800, 800);
        creator.Open();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();

            var logLevelEnv = Environment.GetEnvironmentVariable("REX_SANDBOX_CLIENT_LOG_LEVEL")?.Trim();
            if (!string.IsNullOrEmpty(logLevelEnv) &&
                Enum.TryParse<LogLevel>(logLevelEnv, true, out var logLevel))
            {
                builder.SetMinimumLevel(logLevel);
            }
            else
            {
                // Keep third party and engine noise down, but still show Sandbox client lifecycle (connect, accept, etc.).
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Rex.Sandbox.Client", LogLevel.Information);
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
        using var shutdownHook = new ConsoleShutdownHook(cts, app.Stop);

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

    private static ReadyChildProcess? StartListenServerProcess(int port, ILogger logger)
    {
        var serverAssemblyPath = RuntimeAssemblyLocator.ResolveServerAssemblyPath(
            ServerAssemblyEnvironmentVariable,
            "Rex.Sandbox.Server.dll",
            "Rex.Sandbox.Client",
            "Rex.Sandbox.Server");
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

        ReadyChildProcess? bridge = null;

        try
        {
            bridge = new ReadyChildProcess(process, logger, SandboxProtocolConstants.ListenProcessReadyLine);

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
