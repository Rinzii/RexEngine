namespace Rex.Shared.Net;

/// <summary>How this process participates in networking for a consumer runtime.</summary>
public enum NetMode
{
    /// <summary>Single-process local runtime with no remote networking.</summary>
    Standalone,

    /// <summary>Remote client connected to a host process.</summary>
    Client,

    /// <summary>Client process that also manages a local host process.</summary>
    ListenServer,

    /// <summary>Headless host process.</summary>
    DedicatedServer
}
