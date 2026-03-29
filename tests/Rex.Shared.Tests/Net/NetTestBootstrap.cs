using Rex.Shared.Net.Messages;

namespace Rex.Shared.Tests.Net;

// Runs NetMessages.RegisterAll once per test process for registry tests.
internal static class NetTestBootstrap
{
    static NetTestBootstrap()
    {
        NetMessages.RegisterAll();
    }

    internal static void EnsureRegistered()
    {
    }
}
