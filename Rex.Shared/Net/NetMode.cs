namespace Rex.Shared.Net;

/// <summary>How this process uses the network stack, comparable to Unreal <c>ENetMode</c>.</summary>
public enum NetMode
{
    /// <summary>Single player. Game world runs in-process, no networking.</summary>
    Standalone,

    /// <summary>Remote client connected to a dedicated or listen server.</summary>
    Client,

    /// <summary>Client that owns a server running as a child process.</summary>
    ListenServer,

    /// <summary>Headless server process. Not selected by current Rex.Client CLI defaults.</summary>
    DedicatedServer
}
