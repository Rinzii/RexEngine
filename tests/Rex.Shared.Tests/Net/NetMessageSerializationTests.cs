using LiteNetLib.Utils;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net;

// Wire format round trips through NetMessageRegistry for engine-owned INetMessage types.
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
    // Ack tick survives the round trip.
    public void RoundTrip_StateAckMessage()
    {
        var original = new StateAckMessage(12345u);
        var typed = Assert.IsType<StateAckMessage>(RoundTrip(original));
        Assert.Equal(original.AcknowledgedTick, typed.AcknowledgedTick);
    }

    [Fact]
    // Bulk init chunk and ack wire types round trip.
    public void RoundTrip_BulkTransfer_messages()
    {
        var transferId = Guid.CreateVersion7();
        const byte dataType = 77;
        var init = new BulkTransferInitMessage(transferId, dataType, 1000, 2000, true, 3);
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
