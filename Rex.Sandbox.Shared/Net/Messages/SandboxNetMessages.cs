using Rex.Shared.Net.Messages;
using Rex.Shared.Net;

namespace Rex.Sandbox.Shared.Net.Messages;

/// <summary>
/// Registers the Sandbox protocol on top of the core message set from the engine.
/// </summary>
public static class SandboxNetMessages
{
    private static bool _registered;
    public static void RegisterAll()
    {
        CoreNetMessages.RegisterAll();

        if (_registered)
        {
            return;
        }

        _registered = true;

        NetMessageRegistry.Register(ConnectRequestMessage.Id, ConnectRequestMessage.Deserialize);
        NetMessageRegistry.Register(ConnectResponseMessage.Id, ConnectResponseMessage.Deserialize);
        NetMessageRegistry.Register(PlayerInputMessage.Id, PlayerInputMessage.Deserialize);
        NetMessageRegistry.Register(WorldSnapshotMessage.Id, WorldSnapshotMessage.Deserialize);
        NetMessageRegistry.Register(EntitySpawnMessage.Id, EntitySpawnMessage.Deserialize);
        NetMessageRegistry.Register(EntityDestroyMessage.Id, EntityDestroyMessage.Deserialize);
    }
}
