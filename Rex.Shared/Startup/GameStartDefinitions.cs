namespace Rex.Shared.Startup;

/// <summary>
/// Names the game assemblies that take part in startup.
/// </summary>
/// <param name="GameName">Public title used in logs.</param>
/// <param name="SharedProject">MSBuild project folder name for shared game code.</param>
/// <param name="ClientProject">MSBuild project folder name for the game client.</param>
/// <param name="ServerProject">MSBuild project folder name for the game server.</param>
public sealed record GameRuntimeIdentity(
    string GameName,
    string SharedProject,
    string ClientProject,
    string ServerProject);

/// <summary>
/// Describes the game window contract.
/// </summary>
/// <param name="Title">Initial window title text.</param>
/// <param name="Width">Initial client width in pixels.</param>
/// <param name="Height">Initial client height in pixels.</param>
public sealed record GameWindowDefinition(
    string Title,
    int Width,
    int Height);

/// <summary>
/// Describes how a local server process is located and observed.
/// </summary>
/// <param name="ServerAssemblyEnvironmentVariable">Optional env var holding an absolute path override.</param>
/// <param name="ServerAssemblyFileName">File name searched next to the client build output.</param>
/// <param name="ReadyLine">Stdout substring that marks when the child is accepting connections.</param>
/// <param name="LocalHost">Address passed to the child for bind hints.</param>
/// <param name="StartupTimeoutSeconds">Upper bound on waiting for <paramref name="ReadyLine"/>.</param>
public sealed record ListenServerDefinition(
    string ServerAssemblyEnvironmentVariable,
    string ServerAssemblyFileName,
    string ReadyLine,
    string LocalHost = "127.0.0.1",
    int StartupTimeoutSeconds = 10);

/// <summary>
/// All client bootstrap values the game passes into the engine.
/// </summary>
/// <param name="Identity">Assembly names used for logging and path probing.</param>
/// <param name="DefaultHost">Connect target when the CLI does not pass <c>--connect</c>.</param>
/// <param name="DefaultPort">UDP port baseline before CLI overrides.</param>
/// <param name="TickRate">Fixed simulation rate in Hz.</param>
/// <param name="Window">Initial window parameters.</param>
/// <param name="ListenServer">Child process wiring when the client hosts a local server process.</param>
public sealed record GameClientStartDefinition(
    GameRuntimeIdentity Identity,
    string DefaultHost,
    int DefaultPort,
    int TickRate,
    GameWindowDefinition Window,
    ListenServerDefinition ListenServer);

/// <summary>
/// All server bootstrap values the game passes into the engine.
/// </summary>
/// <param name="Identity">Assembly names used for logging.</param>
/// <param name="DedicatedServerName">Display name for dedicated server logs.</param>
/// <param name="ReadyLine">Stdout line written once the socket is listening.</param>
/// <param name="DefaultPort">UDP port baseline before CLI overrides.</param>
/// <param name="TickRate">Fixed simulation rate in Hz.</param>
/// <param name="MaxPlayers">Session capacity baseline before CLI overrides.</param>
public sealed record GameServerStartDefinition(
    GameRuntimeIdentity Identity,
    string DedicatedServerName,
    string ReadyLine,
    int DefaultPort,
    int TickRate,
    int MaxPlayers);
