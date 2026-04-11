using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// Registers the Sandbox protocol on top of the core message set from the engine.
/// </summary>
public static class SandboxNetMessages
{
    private static bool s_registered;

    public static void RegisterAll()
    {
        CoreNetMessages.RegisterAll();

        if (s_registered)
        {
            return;
        }

        s_registered = true;

        NetMessageRegistry.Register(ConnectRequestMessage.Id, ConnectRequestMessage.Deserialize);
        NetMessageRegistry.Register(ConnectResponseMessage.Id, ConnectResponseMessage.Deserialize);
        NetMessageRegistry.Register(PlayerInputMessage.Id, PlayerInputMessage.Deserialize);
        NetMessageRegistry.Register(WorldSnapshotMessage.Id, WorldSnapshotMessage.Deserialize);
        NetMessageRegistry.Register(EntitySpawnMessage.Id, EntitySpawnMessage.Deserialize);
        NetMessageRegistry.Register(EntityDestroyMessage.Id, EntityDestroyMessage.Deserialize);
    }
}
