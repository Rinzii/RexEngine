using System.Diagnostics.CodeAnalysis;
using Rex.Shared.Net;
using Rex.Shared.Utility;

namespace Rex.Server;

/// <summary>Server CLI. Unknown tokens are collected; the host logs them after bootstrap logging is available.</summary>
internal sealed class CommandLineArgs
{
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
        var port = ProtocolConstants.DefaultPort;
        var maxPlayers = ProtocolConstants.DefaultMaxPlayers;
        var tickRate = ProtocolConstants.DefaultTickRate;

        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            if (arg == "--config-file")
            {
                if (!enumerator.MoveNext())
                {
                    error = "Missing value for --config-file.";
                    return false;
                }

                configFile = enumerator.Current;
            }
            else if (arg == "--data-dir")
            {
                if (!enumerator.MoveNext())
                {
                    error = "Missing value for --data-dir.";
                    return false;
                }

                dataDir = enumerator.Current;
            }
            else if (arg == "--port")
            {
                if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out port))
                {
                    error = "Missing or invalid value for --port.";
                    return false;
                }
            }
            else if (arg == "--max-players")
            {
                if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out maxPlayers))
                {
                    error = "Missing or invalid value for --max-players.";
                    return false;
                }
            }
            else if (arg == "--tick-rate")
            {
                if (!enumerator.MoveNext() || !int.TryParse(enumerator.Current, out tickRate))
                {
                    error = "Missing or invalid value for --tick-rate.";
                    return false;
                }
            }
            else if (arg == "--cvar")
            {
                if (!enumerator.MoveNext())
                {
                    error = "Missing value for --cvar.";
                    return false;
                }

                var cvar = enumerator.Current;
                DebugTools.AssertNotNull(cvar);
                var pos = cvar.IndexOf('=');

                if (pos == -1)
                {
                    error = "Expected key=value after --cvar.";
                    return false;
                }

                cvars.Add((cvar[..pos], cvar[(pos + 1)..]));
            }
            else if (arg == "--logLevel")
            {
                if (!enumerator.MoveNext())
                {
                    error = "Missing value for --logLevel.";
                    return false;
                }

                var logLevel = enumerator.Current;
                DebugTools.AssertNotNull(logLevel);
                var pos = logLevel.IndexOf('=');

                if (pos == -1)
                {
                    error = "Expected key=value after --logLevel.";
                    return false;
                }

                loglevels.Add((logLevel[..pos], logLevel[(pos + 1)..]));
            }
            else if (arg.StartsWith('+'))
            {
                execCommands.Add(arg[1..]);
            }
            else
            {
                unrecognized.Add(arg);
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

    private CommandLineArgs(
        string? configFile,
        string? dataDir,
        IReadOnlyCollection<(string, string)> cVars,
        IReadOnlyCollection<(string, string)> logLevels,
        IReadOnlyList<string> execCommands,
        int port,
        int maxPlayers,
        int tickRate,
        IReadOnlyList<string> unrecognizedArguments
    )
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
}
