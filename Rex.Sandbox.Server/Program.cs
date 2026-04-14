using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Rex.Sandbox.Server.Core;
using Rex.Sandbox.Server.Simulation;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Startup;
using Rex.Shared.Utility;

namespace Rex.Sandbox.Server;

/// <summary>Sandbox server CLI entry.</summary>
internal static class Program
{
    internal static void Main(string[] args)
    {
        using ILoggerFactory loggerFactory = ConsoleStartupSupport.CreateLoggerFactory();

        ILogger bootstrapLogger = loggerFactory.CreateLogger("Rex.Sandbox.Server");

        if (!CommandLineArgs.TryParse(args, out CommandLineArgs? parsed, out string? parseError))
        {
            bootstrapLogger.CliParseFailed(parseError);
            return;
        }

        foreach (string arg in parsed.UnrecognizedArguments)
        {
            bootstrapLogger.UnrecognizedCliArgument(arg);
        }

        var config = new GameServerConfig
        {
            Port = parsed.Port,
            MaxPlayers = parsed.MaxPlayers,
            TickRate = parsed.TickRate,
            ServerName = "Rex Sandbox Dedicated Server"
        };

        using var app = new ServerApp(config, loggerFactory);
        using var cts = new CancellationTokenSource();
        using var shutdownHook = new ConsoleShutdownHook(cts, app.Stop);

        try
        {
            app.Run(cts.Token);
        }
        catch (PortAlreadyInUseException ex)
        {
            bootstrapLogger.PortAlreadyInUse(ex.Message);
            Environment.Exit(1);
        }
    }
}

internal sealed class CommandLineArgs
{
    private CommandLineArgs(
        string? configFile,
        string? dataDir,
        IReadOnlyCollection<(string, string)> cVars,
        IReadOnlyCollection<(string, string)> logLevels,
        IReadOnlyList<string> execCommands,
        int port,
        int maxPlayers,
        int tickRate,
        IReadOnlyList<string> unrecognizedArguments)
    {
        ConfigFile = configFile;
        DataDir = dataDir;
        CVars = cVars;
        LogLevels = logLevels;
        ExecCommands = execCommands;
        Port = port;
        MaxPlayers = maxPlayers;
        TickRate = tickRate;
        UnrecognizedArguments = unrecognizedArguments;
    }

    public string? ConfigFile { get; }
    public string? DataDir { get; }
    public IReadOnlyCollection<(string key, string value)> CVars { get; }
    public IReadOnlyCollection<(string key, string value)> LogLevels { get; }
    public IReadOnlyList<string> ExecCommands { get; set; }
    public int Port { get; }
    public int MaxPlayers { get; }
    public int TickRate { get; }
    public IReadOnlyList<string> UnrecognizedArguments { get; }

    public static bool TryParse(
        IReadOnlyList<string> args,
        [NotNullWhen(true)] out CommandLineArgs? parsed,
        [NotNullWhen(false)] out string? error)
    {
        parsed = null;
        error = null;
        string? configFile = null;
        string? dataDir = null;
        var cvars = new List<(string, string)>();
        var loglevels = new List<(string, string)>();
        var execCommands = new List<string>();
        var unrecognized = new List<string>();
        int port = ProtocolConstants.DefaultPort;
        int maxPlayers = ProtocolConstants.DefaultMaxPlayers;
        int tickRate = ProtocolConstants.DefaultTickRate;

        using IEnumerator<string> enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            string arg = enumerator.Current;
            switch (arg)
            {
                case "--config-file" when !enumerator.MoveNext():
                    error = "Missing value for --config-file.";
                    return false;
                case "--config-file":
                    configFile = enumerator.Current;
                    break;
                case "--data-dir" when !enumerator.MoveNext():
                    error = "Missing value for --data-dir.";
                    return false;
                case "--data-dir":
                    dataDir = enumerator.Current;
                    break;
                case "--port":
                    if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out port))
                    {
                        error = "Missing or invalid value for --port.";
                        return false;
                    }

                    break;
                case "--max-players":
                    if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out maxPlayers))
                    {
                        error = "Missing or invalid value for --max-players.";
                        return false;
                    }

                    break;
                case "--tick-rate":
                    if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out tickRate))
                    {
                        error = "Missing or invalid value for --tick-rate.";
                        return false;
                    }

                    break;
                case "--cvar" when !enumerator.MoveNext():
                    error = "Missing value for --cvar.";
                    return false;
                case "--cvar":
                    {
                        string cvar = enumerator.Current;
                        DebugTools.AssertNotNull(cvar);
                        int pos = cvar.IndexOf('=');

                        if (pos == -1)
                        {
                            error = "Expected key=value after --cvar.";
                            return false;
                        }

                        cvars.Add((cvar[..pos], cvar[(pos + 1)..]));
                        break;
                    }
                case "--logLevel" when !enumerator.MoveNext():
                    error = "Missing value for --logLevel.";
                    return false;
                case "--logLevel":
                    {
                        string logLevel = enumerator.Current;
                        DebugTools.AssertNotNull(logLevel);
                        int pos = logLevel.IndexOf('=');

                        if (pos == -1)
                        {
                            error = "Expected key=value after --logLevel.";
                            return false;
                        }

                        loglevels.Add((logLevel[..pos], logLevel[(pos + 1)..]));
                        break;
                    }
                default:
                    if (arg.StartsWith('+'))
                    {
                        execCommands.Add(arg[1..]);
                    }
                    else
                    {
                        unrecognized.Add(arg);
                    }

                    break;
            }
        }

        parsed = new CommandLineArgs(
            configFile,
            dataDir,
            cvars,
            loglevels,
            execCommands,
            port,
            maxPlayers,
            tickRate,
            unrecognized);

        return true;
    }
}

internal static partial class ServerProgramLog
{
    [LoggerMessage(EventId = LogEventIds.ServerHost.CliParseFailed, Level = LogLevel.Error,
        Message = "Command-line parse failed: {Reason}")]
    public static partial void CliParseFailed(this ILogger logger, string reason);

    [LoggerMessage(EventId = LogEventIds.ServerHost.UnrecognizedCliArgument, Level = LogLevel.Warning,
        Message = "Ignoring unrecognized command-line argument: {Argument}")]
    public static partial void UnrecognizedCliArgument(this ILogger logger, string argument);

    [LoggerMessage(EventId = LogEventIds.ServerHost.PortAlreadyInUse, Level = LogLevel.Error,
        Message = "Dedicated server could not start: {Detail}")]
    public static partial void PortAlreadyInUse(this ILogger logger, string detail);
}
