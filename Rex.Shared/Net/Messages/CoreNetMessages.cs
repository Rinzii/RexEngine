using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Net.Messages;

/// <summary>Registers engine wire payloads for disconnect, snapshot ack and bulk transfer. Games register their own ids elsewhere.</summary>
public static class CoreNetMessages
{
    private static bool _registered;

    /// <summary>Wires disconnect, state ack and bulk transfer deserializers. Later calls return immediately.</summary>
    public static void RegisterAll()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        NetMessageRegistry.Register(DisconnectMessage.Id, DisconnectMessage.Deserialize);
        NetMessageRegistry.Register(StateAckMessage.Id, StateAckMessage.Deserialize);

        NetMessageRegistry.Register(BulkTransferInitMessage.Id, BulkTransferInitMessage.Deserialize);
        NetMessageRegistry.Register(BulkTransferChunkMessage.Id, BulkTransferChunkMessage.Deserialize);
        NetMessageRegistry.Register(BulkTransferAckMessage.Id, BulkTransferAckMessage.Deserialize);
    }
}
