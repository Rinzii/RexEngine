using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// Runs CoreNetMessages.RegisterAll once per test process for engine registry tests.
internal static class NetTestBootstrap
{
    static NetTestBootstrap()
    {
        CoreNetMessages.RegisterAll();
    }

    // No-op call that triggers the static ctor on first use.
    internal static void EnsureRegistered()
    {
    }
}
