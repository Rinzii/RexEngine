using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Net.Messages;

/// <summary>
/// Registers engine-owned networking messages that are reusable across sandbox or future game consumers.
/// Consumer-specific protocols should register their own messages separately.
/// </summary>
public static class CoreNetMessages
{
    private static bool _registered;

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
