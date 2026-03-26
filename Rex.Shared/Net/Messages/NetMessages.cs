using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Registers the built-in networking messages with <see cref="NetMessageRegistry"/>.
/// </summary>
public static class NetMessages
{
    private static bool _registered;

    /// <summary>
    /// Registers all message deserializers.
    /// Safe to call more than once.
    /// </summary>
    public static void RegisterAll()
    {
        if (_registered)
            return;

        _registered = true;

        // Order does not matter. Ids must be unique across this block.
        NetMessageRegistry.Register(ConnectRequestMessage.Id, ConnectRequestMessage.Deserialize);
        NetMessageRegistry.Register(ConnectResponseMessage.Id, ConnectResponseMessage.Deserialize);
        NetMessageRegistry.Register(DisconnectMessage.Id, DisconnectMessage.Deserialize);
        NetMessageRegistry.Register(StateAckMessage.Id, StateAckMessage.Deserialize);

        NetMessageRegistry.Register(PlayerInputMessage.Id, PlayerInputMessage.Deserialize);
        NetMessageRegistry.Register(WorldSnapshotMessage.Id, WorldSnapshotMessage.Deserialize);
        NetMessageRegistry.Register(EntitySpawnMessage.Id, EntitySpawnMessage.Deserialize);
        NetMessageRegistry.Register(EntityDestroyMessage.Id, EntityDestroyMessage.Deserialize);

        NetMessageRegistry.Register(BulkTransferInitMessage.Id, BulkTransferInitMessage.Deserialize);
        NetMessageRegistry.Register(BulkTransferChunkMessage.Id, BulkTransferChunkMessage.Deserialize);
        NetMessageRegistry.Register(BulkTransferAckMessage.Id, BulkTransferAckMessage.Deserialize);
    }
}