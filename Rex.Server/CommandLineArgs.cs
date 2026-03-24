using System.Diagnostics.CodeAnalysis;
using Rex.Shared.Utility;
using C = System.Console;

namespace Rex.Server;

internal sealed class CommandLineArgs
{
    public string? ConfigFile { get; }
    public string? DataDir { get; }
    public IReadOnlyCollection<(string key, string value)> CVars { get; }
    public IReadOnlyCollection<(string key, string value)> LogLevels { get; }
    public IReadOnlyList<string> ExecCommands { get; set; }
    
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CommandLineArgs? parsed)
    {
        parsed = null;
        string? configFile = null;
        string? dataDir = null;
        var cvars = new List<(string, string)>();
        var loglevels = new List<(string, string)>();
        var execCommands = new List<string>();

        using var enumerator = args.GetEnumerator();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            if (arg == "--config-file")
            {
                if (!enumerator.MoveNext())
                {
                    C.WriteLine("Missing config file!");
                    return false;
                }

                configFile = enumerator.Current;
            }
            else if (arg == "--data-dir")
            {
                if (!enumerator.MoveNext())
                {
                    C.WriteLine("Missing data directory!");
                    return false;
                }

                dataDir = enumerator.Current;
            }
            else if (arg == "--cvar")
            {
                if (!enumerator.MoveNext())
                {
                    C.WriteLine("Missing cvar value!");
                    return false;
                }

                var cvar = enumerator.Current;
                DebugTools.AssertNotNull(cvar);
                var pos = cvar.IndexOf('=');

                if (pos == -1)
                {
                    C.WriteLine("Expected = in cvar!");
                    return false;
                }
                
                cvars.Add((cvar[..pos], cvar[(pos + 1)..]));
            }
            else if (arg == "--logLevel")
            {
                if (!enumerator.MoveNext())
                {
                    C.WriteLine("Missing cvar value!");
                    return false;
                }

                var logLevel = enumerator.Current;
                DebugTools.AssertNotNull(logLevel);
                var pos = logLevel.IndexOf('=');

                if (pos == -1)
                {
                    C.WriteLine("Expected = in cvar!");
                    return false;
                }
                
                loglevels.Add((logLevel[..pos], logLevel[(pos + 1)..]));
            }
            else if (arg.StartsWith("+"))
            {
                execCommands.Add(arg[1..]);
            }
            else
            {
                C.WriteLine("Unknown argument: {0}", arg);
            }
            
        }

        parsed = new CommandLineArgs(configFile, dataDir, cvars, loglevels, execCommands);
        
        return true;
    }

    private CommandLineArgs(
        string? configFile,
        string? dataDir,
        IReadOnlyCollection<(string, string)> cVars,
        IReadOnlyCollection<(string, string)> logLevels,
        IReadOnlyList<string> execCommands
    )
    {
        ConfigFile = configFile;
        DataDir = dataDir;
        CVars = cVars;
        LogLevels = logLevels;
        ExecCommands = execCommands;
    }
}