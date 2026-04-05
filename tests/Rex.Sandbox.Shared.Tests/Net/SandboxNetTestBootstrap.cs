using Rex.Sandbox.Shared.Net.Messages;

namespace Rex.Sandbox.Shared.Tests.Net;

// Runs SandboxNetMessages.RegisterAll once per test process for sandbox protocol tests.
internal static class SandboxNetTestBootstrap
{
    static SandboxNetTestBootstrap()
    {
        SandboxNetMessages.RegisterAll();
    }

    internal static void EnsureRegistered()
    {
    }
}
