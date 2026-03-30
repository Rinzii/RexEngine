using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net;

// Wire format round trips through NetMessageRegistry for each INetMessage type.
public sealed class NetMessageSerializationTests
{
    public NetMessageSerializationTests()
    {
        NetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    // Disconnect reason survives the round trip.
    public void RoundTrip_DisconnectMessage()
    {
        var original = new DisconnectMessage("server shutdown");
        var decoded = RoundTrip(original);
        var typed = Assert.IsType<DisconnectMessage>(decoded);
        Assert.Equal(original.Reason, typed.Reason);
    }

    [Fact]
    // All input axes and flags survive the round trip.
    public void RoundTrip_PlayerInputMessage()
    {
        var original = new PlayerInputMessage(99, 0.25f, -1f, 3.14f, 0.5f, 0xABCD);
        var decoded = RoundTrip(original);
        var typed = Assert.IsType<PlayerInputMessage>(decoded);
        Assert.Equal(original.Tick, typed.Tick);
        Assert.Equal(original.MoveX, typed.MoveX);
        Assert.Equal(original.MoveY, typed.MoveY);
        Assert.Equal(original.LookX, typed.LookX);
        Assert.Equal(original.LookY, typed.LookY);
        Assert.Equal(original.ActionFlags, typed.ActionFlags);
    }

    [Fact]
    // Accepted and rejected responses both round trip including optional reject text.
    public void RoundTrip_ConnectResponse_accepted_and_rejected()
    {
        var clientId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
        var accepted = new ConnectResponseMessage(true, clientId, 60, 7, null);
        var decodedOk = Assert.IsType<ConnectResponseMessage>(RoundTrip(accepted));
        Assert.True(decodedOk.Accepted);
        Assert.Equal(clientId, decodedOk.ClientId);
        Assert.Equal(60, decodedOk.TickRate);
        Assert.Equal(7, decodedOk.LocalPlayerEntityId);
        Assert.Null(decodedOk.RejectReason);

        var rejected = new ConnectResponseMessage(false, Guid.Empty, 0, 0, "full");
        var decodedNo = Assert.IsType<ConnectResponseMessage>(RoundTrip(rejected));
        Assert.False(decodedNo.Accepted);
        Assert.Equal("full", decodedNo.RejectReason);
    }

    [Fact]
    // Ack tick survives the round trip.
    public void RoundTrip_StateAckMessage()
    {
        var original = new StateAckMessage(12345u);
        var typed = Assert.IsType<StateAckMessage>(RoundTrip(original));
        Assert.Equal(original.AcknowledgedTick, typed.AcknowledgedTick);
    }

    [Fact]
    // Spawn fields and owner id survive the round trip.
    public void RoundTrip_EntitySpawnMessage()
    {
        var owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var entityType = "TestEntity";
        var original = new EntitySpawnMessage(42, owner, entityType, 1f, 2f, 3f);
        var typed = Assert.IsType<EntitySpawnMessage>(RoundTrip(original));
        Assert.Equal(original.EntityId, typed.EntityId);
        Assert.Equal(original.OwnerClientId, typed.OwnerClientId);
        Assert.Equal(original.EntityType, typed.EntityType);
        Assert.Equal(original.X, typed.X);
        Assert.Equal(original.Y, typed.Y);
        Assert.Equal(original.Z, typed.Z);
    }

    [Fact]
    // Destroy id survives the round trip.
    public void RoundTrip_EntityDestroyMessage()
    {
        var original = new EntityDestroyMessage(9001);
        var typed = Assert.IsType<EntityDestroyMessage>(RoundTrip(original));
        Assert.Equal(original.EntityId, typed.EntityId);
    }

    [Fact]
    // Empty snapshot and multi-entity snapshot both round trip.
    public void RoundTrip_WorldSnapshotMessage_empty_and_multiple_entities()
    {
        var empty = new WorldSnapshotMessage(10u, 9u, Array.Empty<EntityState>());
        var decodedEmpty = Assert.IsType<WorldSnapshotMessage>(RoundTrip(empty));
        Assert.Equal(10u, decodedEmpty.ServerTick);
        Assert.Equal(9u, decodedEmpty.LastProcessedInputTick);
        Assert.Empty(decodedEmpty.Entities);

        var entities = new EntityState[]
        {
            new(1, 0f, 0f, 0f, 0f),
            new(2, 10f, 20f, 30f, 45f)
        };
        var multi = new WorldSnapshotMessage(100u, 99u, entities);
        var decodedMulti = Assert.IsType<WorldSnapshotMessage>(RoundTrip(multi));
        Assert.Equal(2, decodedMulti.Entities.Count);
        Assert.Equal(1, decodedMulti.Entities[0].EntityId);
        Assert.Equal(45f, decodedMulti.Entities[1].RotationY);
    }

    [Fact]
    // Bulk init chunk and ack wire types round trip.
    public void RoundTrip_BulkTransfer_messages()
    {
        var transferId = Guid.CreateVersion7();
        var init = new BulkTransferInitMessage(transferId, BulkDataType.MapData, 1000, 2000, true, 3);
        var decodedInit = Assert.IsType<BulkTransferInitMessage>(RoundTrip(init));
        Assert.Equal(init.TransferId, decodedInit.TransferId);
        Assert.Equal(init.DataType, decodedInit.DataType);
        Assert.Equal(init.TotalSize, decodedInit.TotalSize);
        Assert.Equal(init.OriginalSize, decodedInit.OriginalSize);
        Assert.Equal(init.IsCompressed, decodedInit.IsCompressed);
        Assert.Equal(init.ChunkCount, decodedInit.ChunkCount);

        var chunk = new BulkTransferChunkMessage(transferId, 1, new byte[] { 1, 2, 3 });
        var decodedChunk = Assert.IsType<BulkTransferChunkMessage>(RoundTrip(chunk));
        Assert.Equal(chunk.TransferId, decodedChunk.TransferId);
        Assert.Equal(chunk.ChunkIndex, decodedChunk.ChunkIndex);
        Assert.Equal(chunk.Data, decodedChunk.Data);

        var ack = new BulkTransferAckMessage(transferId, true);
        var decodedAck = Assert.IsType<BulkTransferAckMessage>(RoundTrip(ack));
        Assert.Equal(ack.TransferId, decodedAck.TransferId);
        Assert.Equal(ack.Success, decodedAck.Success);
    }

    // Writes the full packet and reads it back through the registry.
    private static INetMessage RoundTrip(INetMessage original)
    {
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);
        return NetMessageRegistry.Deserialize(reader);
    }
}
