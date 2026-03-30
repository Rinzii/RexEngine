using LiteNetLib;
using Microsoft.Extensions.Logging.Abstractions;
using Rex.Shared.Net;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// Chunked bulk send and inbound reassembly.
public sealed class BulkTransferManagerTests
{
    [Fact]
    // Two chunks applied second then first still yields the full byte array.
    public void HandleTransfer_manual_two_chunks_out_of_order_reassembles_payload()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var transferId = Guid.CreateVersion7();
        var payload = new byte[5000];
        Random.Shared.NextBytes(payload);

        byte[]? received = null;
        manager.TransferCompleted += (_, dataType, raw) =>
        {
            Assert.Equal(BulkDataType.ResourceFile, dataType);
            received = raw;
        };

        var first = new byte[BulkTransferManager.MaxChunkSize];
        var second = new byte[payload.Length - first.Length];
        Buffer.BlockCopy(payload, 0, first, 0, first.Length);
        Buffer.BlockCopy(payload, first.Length, second, 0, second.Length);

        var init = new BulkTransferInitMessage(transferId, BulkDataType.ResourceFile, payload.Length, payload.Length,
            false, 2);
        manager.HandleTransferInit(init);
        manager.HandleTransferChunk(new BulkTransferChunkMessage(transferId, 1, second));
        manager.HandleTransferChunk(new BulkTransferChunkMessage(transferId, 0, first));

        Assert.NotNull(received);
        Assert.Equal(payload, received);
    }

    [Fact]
    // SendBulkData over a fake channel then HandleTransfer reproduces MapData.
    public void SendBulkData_small_MapData_round_trips_through_handler()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var channel = new RecordingServerChannel();
        var map = new MapData { MapName = "tiny" };

        MapData? received = null;
        manager.TransferCompleted += (_, _, payload) =>
        {
            received = ProtoSerializer.Deserialize<MapData>(payload);
        };

        manager.SendBulkData(channel, BulkDataType.MapData, map);

        var init = channel.Sent.OfType<BulkTransferInitMessage>().Single();
        manager.HandleTransferInit(init);
        foreach (var chunk in channel.Sent.OfType<BulkTransferChunkMessage>())
        {
            manager.HandleTransferChunk(chunk);
        }

        Assert.NotNull(received);
        Assert.Equal("tiny", received!.MapName);
    }

    [Fact]
    // Orphan chunk does not raise TransferCompleted.
    public void HandleTransferChunk_without_init_does_not_complete()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var fired = false;
        manager.TransferCompleted += (_, _, _) => fired = true;

        var chunk = new BulkTransferChunkMessage(Guid.CreateVersion7(), 0, new byte[] { 1 });
        manager.HandleTransferChunk(chunk);

        Assert.False(fired);
    }

    // Records outbound messages from SendBulkData.
    private sealed class RecordingServerChannel : IServerNetChannel
    {
        public List<INetMessage> Sent { get; } = new();

        public Guid ClientId => Guid.Empty;

        public bool IsLocal => true;

        public Rex.Shared.Net.ConnectionState State { get; set; }

        public int RoundTripTimeMs => 0;

        public void Send(INetMessage message, byte channel, DeliveryMethod delivery)
        {
            Sent.Add(message);
        }

        public void Send(INetMessage message)
        {
            Sent.Add(message);
        }

        public void Disconnect(string reason)
        {
        }
    }
}
