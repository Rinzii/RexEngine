namespace Rex.Sandbox.Shared.Net;

/// <summary>
/// Protocol values for the Sandbox sample. They model what a future external game repository would own for its wire contract.
/// </summary>
public static class SandboxProtocolConstants
{
    public const string ConnectionKey = "RexSandbox";
    public const string ListenProcessReadyLine = "RexSandbox listen-ready v1";
}
