using System.Diagnostics.CodeAnalysis;
using Rex.Shared.Net;

namespace Rex.Client;

/// <summary>Flags from argv. Defaults to standalone if neither --connect nor --listen.</summary>
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
