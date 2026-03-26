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

    internal CommandLineArgs(bool headless, NetMode mode, string? connectAddress, int port)
    {
        Headless = headless;
        Mode = mode;
        ConnectAddress = connectAddress;
        Port = port;
    }

    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CommandLineArgs? parsed)
    {
        parsed = null;
        var headless = false;
        var listenServer = false;
        var standalone = false;
        string? connectAddress = null;
        var port = ProtocolConstants.DefaultPort;

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
                    Console.WriteLine("Missing connect address!");
                    return false;
                case "--connect":
                    connectAddress = enumerator.Current;
                    break;
                case "--port" when !enumerator.MoveNext() || !int.TryParse(enumerator.Current, out port):
                    Console.WriteLine("Missing or invalid port!");
                    return false;
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

        parsed = new CommandLineArgs(headless, mode, connectAddress, port);
        return true;
    }
}
