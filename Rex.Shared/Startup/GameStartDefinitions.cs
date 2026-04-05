namespace Rex.Shared.Startup;

/// <summary>
/// Names the game assemblies that take part in startup.
/// </summary>
public sealed record GameRuntimeIdentity(
    string GameName,
    string SharedProject,
    string ClientProject,
    string ServerProject);

/// <summary>
/// Describes the game window contract.
/// </summary>
public sealed record GameWindowDefinition(
    string Title,
    int Width,
    int Height);

/// <summary>
/// Describes how a local server process is located and observed.
/// </summary>
public sealed record ListenServerDefinition(
    string ServerAssemblyEnvironmentVariable,
    string ServerAssemblyFileName,
    string ReadyLine,
    string LocalHost = "127.0.0.1",
    int StartupTimeoutSeconds = 10);

/// <summary>
/// Supplies game specific client startup values.
/// </summary>
public sealed record GameClientStartDefinition(
    GameRuntimeIdentity Identity,
    string DefaultHost,
    int DefaultPort,
    int TickRate,
    GameWindowDefinition Window,
    ListenServerDefinition ListenServer);

/// <summary>
/// Supplies game specific server startup values.
/// </summary>
public sealed record GameServerStartDefinition(
    GameRuntimeIdentity Identity,
    string DedicatedServerName,
    string ReadyLine,
    int DefaultPort,
    int TickRate,
    int MaxPlayers);
