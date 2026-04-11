namespace Rex.Shared.Logging;

/// <summary>
/// Numeric event ids for <c>LoggerMessage</c> attributes. Each nested type exposes <c>RangeFirst</c>, <c>RangeLast</c>
/// and one constant per event. Most types reserve 100 consecutive ids. <c>ClientHost</c> and <c>GameServerHost</c>
/// reserve 200.
/// </summary>
public static class LogEventIds
{
    /// <summary>Ids for bulk transfer logging.</summary>
    public static class BulkTransfer
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1000;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1099;
        // ReSharper restore UnusedMember.Global

        /// <summary>Outbound bulk transfer started.</summary>
        public const int TransferStarted = 1000;

        /// <summary>Inbound bulk transfer is receiving chunks.</summary>
        public const int TransferReceiving = 1001;

        /// <summary>Chunk referenced an unknown transfer id.</summary>
        public const int UnknownTransferChunk = 1002;

        /// <summary>Bulk transfer finished successfully.</summary>
        public const int TransferComplete = 1003;
    }

    /// <summary>Ids for the networked game client.</summary>
    public static class GameClient
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1100;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1199;
        // ReSharper restore UnusedMember.Global

        /// <summary>Client reached an active server session.</summary>
        public const int ConnectedToServer = 1100;

        /// <summary>Client left or lost the server session.</summary>
        public const int Disconnected = 1101;

        /// <summary>Spawn notification for an entity.</summary>
        public const int EntitySpawned = 1102;

        /// <summary>Destroy notification for an entity.</summary>
        public const int EntityDestroyed = 1103;

        /// <summary>Server refused the connection.</summary>
        public const int ConnectionRejected = 1104;

        /// <summary>Server accepted the connection.</summary>
        public const int ConnectionAccepted = 1105;

        /// <summary>Client finished receiving a bulk payload.</summary>
        public const int ClientBulkTransferComplete = 1106;

        /// <summary>No handler matched an inbound message id.</summary>
        public const int UnhandledNetMessage = 1107;

        /// <summary>Client requested a full world state resend.</summary>
        public const int FullStateRequested = 1108;
    }

    /// <summary>Ids for the remote client transport.</summary>
    public static class ClientTransport
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1200;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1299;
        // ReSharper restore UnusedMember.Global

        /// <summary>LiteNetLib transport failed to start.</summary>
        public const int TransportStartFailed = 1200;

        /// <summary>Inbound bytes could not be deserialized.</summary>
        public const int DeserializeMessageFailed = 1201;

        /// <summary>LiteNetLib reported a transport error from the host socket.</summary>
        public const int NetworkError = 1202;

        /// <summary>LiteNetLib updated a round trip latency sample.</summary>
        public const int LatencyUpdated = 1203;
    }

    /// <summary>Ids for the top level client application.</summary>
    public static class ClientApp
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1300;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1399;
        // ReSharper restore UnusedMember.Global

        /// <summary>Startup options referenced an invalid net mode.</summary>
        public const int InvalidNetMode = 1300;

        /// <summary>Client loop is active.</summary>
        public const int ClientRunning = 1301;

        /// <summary>Standalone world finished booting.</summary>
        public const int StandaloneWorldInitialized = 1302;

        /// <summary>Client <c>OnUpdate</c> threw.</summary>
        public const int OnUpdateFailed = 1303;

        /// <summary>Client <c>OnLateUpdate</c> threw.</summary>
        public const int OnLateUpdateFailed = 1304;

        /// <summary>Main loop stopped due to cancellation.</summary>
        public const int MainLoopCancellationRequested = 1305;
    }

    /// <summary>Ids for the client entry point and for wiring the listen server child process.</summary>
    public static class ClientHost
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1400;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1599;
        // ReSharper restore UnusedMember.Global

        /// <summary>User requested shutdown from the console hook.</summary>
        public const int ShutdownSignal = 1400;

        /// <summary>Listen server child printed stdout.</summary>
        public const int ListenServerStdout = 1401;

        /// <summary>Listen server child printed stderr.</summary>
        public const int ListenServerStderr = 1402;

        /// <summary>No server assembly matched the lookup rules.</summary>
        public const int ListenServerAssemblyNotFound = 1403;

        /// <summary>Child process failed to start.</summary>
        public const int ListenServerStartFailed = 1404;

        /// <summary>Child exited before ready.</summary>
        public const int ListenServerExitedEarly = 1405;

        /// <summary>Ready line never arrived.</summary>
        public const int ListenServerStartupTimeout = 1406;

        /// <summary>Killing the listen server child.</summary>
        public const int StoppingListenServer = 1407;

        /// <summary>Connect address text was invalid.</summary>
        public const int InvalidConnectAddress = 1408;

        /// <summary>CLI parsing failed.</summary>
        public const int CliParseFailed = 1409;

        /// <summary>Unknown CLI token.</summary>
        public const int UnrecognizedCliArgument = 1410;
    }

    /// <summary>Ids for the dedicated server entry point.</summary>
    public static class ServerHost
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1600;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1699;
        // ReSharper restore UnusedMember.Global

        /// <summary>CLI parsing failed.</summary>
        public const int CliParseFailed = 1600;

        /// <summary>Unknown CLI token.</summary>
        public const int UnrecognizedCliArgument = 1601;

        /// <summary>UDP bind failed because the port is taken.</summary>
        public const int PortAlreadyInUse = 1602;
    }

    /// <summary>Ids for the server network layer.</summary>
    public static class GameServerNet
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1700;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1799;
        // ReSharper restore UnusedMember.Global

        /// <summary>Listen socket could not open.</summary>
        public const int CannotListenOnPort = 1700;

        /// <summary>Server socket is listening.</summary>
        public const int ServerListening = 1701;

        /// <summary>Network stack stopped.</summary>
        public const int ServerNetworkStopped = 1702;

        /// <summary>Handshake rejected because the session is full.</summary>
        public const int ConnectionRejectedServerFull = 1703;

        /// <summary>Remote peer finished connecting.</summary>
        public const int PeerConnected = 1704;

        /// <summary>Remote peer disconnected.</summary>
        public const int PeerDisconnected = 1705;

        /// <summary>Inbound bytes could not be deserialized.</summary>
        public const int DeserializeMessageFailed = 1706;

        /// <summary>LiteNetLib reported a transport error from the listen socket.</summary>
        public const int NetworkError = 1707;
    }

    /// <summary>Ids for the dedicated server application loop.</summary>
    public static class ServerApp
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1800;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 1899;
        // ReSharper restore UnusedMember.Global

        /// <summary>Dedicated server loop is active.</summary>
        public const int DedicatedServerRunning = 1800;

        /// <summary>User requested shutdown from the console hook.</summary>
        public const int ShutdownSignal = 1801;

        /// <summary>Server <c>OnUpdate</c> threw.</summary>
        public const int OnUpdateFailed = 1802;

        /// <summary>Server <c>OnLateUpdate</c> threw.</summary>
        public const int OnLateUpdateFailed = 1803;
    }

    /// <summary>Ids for the authoritative simulation host.</summary>
    public static class GameServerHost
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>First id in this range.</summary>
        public const int RangeFirst = 1900;

        /// <summary>Last id in this range.</summary>
        public const int RangeLast = 2099;
        // ReSharper restore UnusedMember.Global

        /// <summary>Simulation host started accepting sessions.</summary>
        public const int HostStarted = 1900;

        /// <summary>New client session added.</summary>
        public const int SessionAdded = 1901;

        /// <summary>Client session removed.</summary>
        public const int SessionRemoved = 1902;

        /// <summary>Bulk payload was acknowledged.</summary>
        public const int BulkTransferAcked = 1903;

        /// <summary>Client is disconnecting.</summary>
        public const int ClientDisconnecting = 1904;

        /// <summary>Host is shutting down.</summary>
        public const int HostShuttingDown = 1905;

        /// <summary>Host finished stopping.</summary>
        public const int HostStopped = 1906;

        /// <summary>Client passed authentication.</summary>
        public const int ClientAuthenticated = 1907;

        /// <summary>No handler matched an inbound message id.</summary>
        public const int UnhandledNetMessage = 1908;

        /// <summary>Start was requested while already running.</summary>
        public const int HostAlreadyRunning = 1909;

        /// <summary>Client requested a full state resync.</summary>
        public const int ClientRequestedFullState = 1910;
    }
}
