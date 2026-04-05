using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// Runs CoreNetMessages.RegisterAll once per test process for engine registry tests.
internal static class NetTestBootstrap
{
    static NetTestBootstrap()
    {
        CoreNetMessages.RegisterAll();
    }

    // Call that does nothing except trigger the static constructor on first use.
    internal static void EnsureRegistered()
    {
    }
}
