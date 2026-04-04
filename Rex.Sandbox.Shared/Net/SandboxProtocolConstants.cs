namespace Rex.Sandbox.Shared.Net;

/// <summary>
/// Sandbox-owned protocol values. These model what a future external game repository would own for its own wire contract.
/// </summary>
public static class SandboxProtocolConstants
{
    public const string ConnectionKey = "RexSandbox";
    public const string ListenProcessReadyLine = "RexSandbox listen-ready v1";
}
