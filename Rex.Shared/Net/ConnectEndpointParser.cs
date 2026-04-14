namespace Rex.Shared.Net;

/// <summary>Parses CLI connect strings into host and port. Supports bracketed IPv6, hostnames and IPv4.</summary>
public static class ConnectEndpointParser
{
    /// <summary>Parses <paramref name="connectAddress"/> or returns defaults when null or whitespace.</summary>
    /// <returns>False when the value is present but malformed.</returns>
    public static bool TryParse(string? connectAddress, int defaultPort, out string host, out int port)
    {
        host = "127.0.0.1";
        port = defaultPort;

        if (string.IsNullOrWhiteSpace(connectAddress))
        {
            return true;
        }

        string trimmed = connectAddress.Trim();

        if (trimmed.StartsWith('['))
        {
            return TryParseBracketedIpv6(trimmed, defaultPort, out host, out port);
        }

        int lastColon = trimmed.LastIndexOf(':');
        if (lastColon <= 0)
        {
            host = trimmed;
            port = defaultPort;
            return true;
        }

        string tail = trimmed[(lastColon + 1)..];
        if (tail.Length > 0
            && IsAsciiDigitsOnly(tail)
            && int.TryParse(tail, out int parsedPort)
            && parsedPort is > 0 and <= 65535)
        {
            string hostPart = trimmed[..lastColon];
            if (hostPart.Length == 0)
            {
                return false;
            }

            if (!hostPart.Contains(':'))
            {
                host = hostPart;
                port = parsedPort;
                return true;
            }
        }

        host = trimmed;
        port = defaultPort;
        return true;
    }

    private static bool TryParseBracketedIpv6(string trimmed, int defaultPort, out string host, out int port)
    {
        host = "127.0.0.1";
        port = defaultPort;

        int close = trimmed.IndexOf(']', 1);
        if (close <= 1)
        {
            return false;
        }

        host = trimmed[1..close];
        if (host.Length == 0)
        {
            return false;
        }

        if (close == trimmed.Length - 1)
        {
            port = defaultPort;
            return true;
        }

        if (trimmed[close + 1] != ':')
        {
            return false;
        }

        string portPart = trimmed[(close + 2)..];
        if (portPart.Length == 0
            || !int.TryParse(portPart, out int p)
            || p is <= 0 or > 65535)
        {
            return false;
        }

        port = p;
        return true;
    }

    private static bool IsAsciiDigitsOnly(ReadOnlySpan<char> s)
    {
        foreach (char c in s)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return s.Length > 0;
    }
}
