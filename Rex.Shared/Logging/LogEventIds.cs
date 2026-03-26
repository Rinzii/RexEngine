namespace Rex.Shared.Logging;

/// <summary>
/// Numeric event ids for <c>LoggerMessage</c> attributes. Each nested type exposes <c>RangeFirst</c>, <c>RangeLast</c>,
/// and one constant per event. Most types reserve 100 consecutive ids. <c>ClientHost</c> and <c>GameServerHost</c>
/// reserve 200.
/// </summary>
public static class LogEventIds
{
    /// <summary>Ids for bulk transfer logging.</summary>
    public static class BulkTransfer
    {
        public const int RangeFirst = 1000;
        public const int RangeLast = 1099;

        public const int TransferStarted = 1000;
        public const int TransferReceiving = 1001;
        public const int UnknownTransferChunk = 1002;
        public const int TransferComplete = 1003;
    }

    /// <summary>Ids for the networked game client.</summary>
    public static class GameClient
    {
        public const int RangeFirst = 1100;
        public const int RangeLast = 1199;

        public const int ConnectedToServer = 1100;
        public const int Disconnected = 1101;
        public const int EntitySpawned = 1102;
        public const int EntityDestroyed = 1103;
        public const int ConnectionRejected = 1104;
        public const int ConnectionAccepted = 1105;
        public const int ClientBulkTransferComplete = 1106;
        public const int UnhandledNetMessage = 1107;
    }

    /// <summary>Ids for the remote client transport.</summary>
    public static class ClientTransport
    {
        public const int RangeFirst = 1200;
        public const int RangeLast = 1299;

        public const int TransportStartFailed = 1200;
        public const int DeserializeMessageFailed = 1201;
    }

    /// <summary>Ids for the top-level client application.</summary>
    public static class ClientApp
    {
        public const int RangeFirst = 1300;
        public const int RangeLast = 1399;

        public const int InvalidNetMode = 1300;
        public const int ClientRunning = 1301;
        public const int StandaloneWorldInitialized = 1302;
        public const int OnUpdateFailed = 1303;
        public const int OnLateUpdateFailed = 1304;
        public const int MainLoopCancellationRequested = 1305;
    }

    /// <summary>Ids for the client entry point and listen-server process wiring.</summary>
    public static class ClientHost
    {
        public const int RangeFirst = 1400;
        public const int RangeLast = 1599;

        public const int ShutdownSignal = 1400;
        public const int ListenServerStdout = 1401;
        public const int ListenServerStderr = 1402;
        public const int ListenServerAssemblyNotFound = 1403;
        public const int ListenServerStartFailed = 1404;
        public const int ListenServerExitedEarly = 1405;
        public const int ListenServerStartupTimeout = 1406;
        public const int StoppingListenServer = 1407;
        public const int InvalidConnectAddress = 1408;
        public const int CliParseFailed = 1409;
        public const int UnrecognizedCliArgument = 1410;
    }

    /// <summary>Ids for the dedicated server entry point.</summary>
    public static class ServerHost
    {
        public const int RangeFirst = 1600;
        public const int RangeLast = 1699;

        public const int CliParseFailed = 1600;
        public const int UnrecognizedCliArgument = 1601;
        public const int PortAlreadyInUse = 1602;
    }

    /// <summary>Ids for the server network layer.</summary>
    public static class GameServerNet
    {
        public const int RangeFirst = 1700;
        public const int RangeLast = 1799;

        public const int CannotListenOnPort = 1700;
        public const int ServerListening = 1701;
        public const int ServerNetworkStopped = 1702;
        public const int ConnectionRejectedServerFull = 1703;
        public const int PeerConnected = 1704;
        public const int PeerDisconnected = 1705;
        public const int DeserializeMessageFailed = 1706;
    }

    /// <summary>Ids for the dedicated server application loop.</summary>
    public static class ServerApp
    {
        public const int RangeFirst = 1800;
        public const int RangeLast = 1899;

        public const int DedicatedServerRunning = 1800;
        public const int ShutdownSignal = 1801;
        public const int OnUpdateFailed = 1802;
        public const int OnLateUpdateFailed = 1803;
    }

    /// <summary>Ids for the authoritative simulation host.</summary>
    public static class GameServerHost
    {
        public const int RangeFirst = 1900;
        public const int RangeLast = 2099;

        public const int HostStarted = 1900;
        public const int SessionAdded = 1901;
        public const int SessionRemoved = 1902;
        public const int BulkTransferAcked = 1903;
        public const int ClientDisconnecting = 1904;
        public const int HostShuttingDown = 1905;
        public const int HostStopped = 1906;
        public const int ClientAuthenticated = 1907;
        public const int UnhandledNetMessage = 1908;
        public const int HostAlreadyRunning = 1909;
    }
}
