using LiteNetLib;
using Microsoft.Extensions.Logging.Abstractions;
using ProtoBuf;
using Rex.Shared.Net;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// Chunked bulk send and inbound reassembly.
public sealed class BulkTransferManagerTests
{
    private const byte TestDataType = 77;

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
            Assert.Equal(5, dataType);
            received = raw;
        };

        var first = new byte[BulkTransferManager.MaxChunkSize];
        var second = new byte[payload.Length - first.Length];
        Buffer.BlockCopy(payload, 0, first, 0, first.Length);
        Buffer.BlockCopy(payload, first.Length, second, 0, second.Length);

        var init = new BulkTransferInitMessage(transferId, 5, payload.Length, payload.Length,
            false, 2);
        manager.HandleTransferInit(init);
        manager.HandleTransferChunk(new BulkTransferChunkMessage(transferId, 1, second));
        manager.HandleTransferChunk(new BulkTransferChunkMessage(transferId, 0, first));

        Assert.NotNull(received);
        Assert.Equal(payload, received);
    }

    [Fact]
    // SendBulkData over a fake channel then HandleTransfer reproduces a generic payload.
    public void SendBulkData_small_payload_round_trips_through_handler()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var channel = new RecordingServerChannel();
        var payload = new SampleTransferPayload { Name = "tiny" };

        SampleTransferPayload? received = null;
        manager.TransferCompleted += (_, _, payload) =>
        {
            received = ProtoSerializer.Deserialize<SampleTransferPayload>(payload);
        };

        manager.SendBulkData(channel, TestDataType, payload);

        var init = channel.Sent.OfType<BulkTransferInitMessage>().Single();
        manager.HandleTransferInit(init);
        foreach (var chunk in channel.Sent.OfType<BulkTransferChunkMessage>())
        {
            manager.HandleTransferChunk(chunk);
        }

        Assert.NotNull(received);
        Assert.Equal("tiny", received!.Name);
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

    [Fact]
    // IClientNetChannel SendBulkData path completes like the server overload.
    public void SendBulkData_client_channel_round_trips_payload()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var channel = new RecordingClientChannel();
        var payload = new SampleTransferPayload { Name = "client-path" };

        SampleTransferPayload? received = null;
        manager.TransferCompleted += (_, _, payload) =>
        {
            received = ProtoSerializer.Deserialize<SampleTransferPayload>(payload);
        };

        manager.SendBulkData(channel, TestDataType, payload);

        var init = channel.Sent.OfType<BulkTransferInitMessage>().Single();
        manager.HandleTransferInit(init);
        foreach (var chunk in channel.Sent.OfType<BulkTransferChunkMessage>())
        {
            manager.HandleTransferChunk(chunk);
        }

        Assert.NotNull(received);
        Assert.Equal("client-path", received!.Name);
    }

    [Fact]
    // Large repeating generic payload compresses on the wire and reassembles correctly.
    public void SendBulkData_compressed_payload_round_trips_large_payload()
    {
        var manager = new BulkTransferManager(NullLoggerFactory.Instance);
        var channel = new RecordingServerChannel();
        var payload = new SampleTransferPayload { Name = "heavy" };
        for (var i = 0; i < 12_000; i++)
        {
            payload.Numbers.Add(7);
        }

        SampleTransferPayload? received = null;
        manager.TransferCompleted += (_, _, payload) =>
        {
            received = ProtoSerializer.Deserialize<SampleTransferPayload>(payload);
        };

        manager.SendBulkData(channel, TestDataType, payload);

        var init = channel.Sent.OfType<BulkTransferInitMessage>().Single();
        Assert.True(init.IsCompressed, "expected Brotli to shrink this payload");

        manager.HandleTransferInit(init);
        foreach (var chunk in channel.Sent.OfType<BulkTransferChunkMessage>())
        {
            manager.HandleTransferChunk(chunk);
        }

        Assert.NotNull(received);
        Assert.Equal(12_000, received!.Numbers.Count);
        Assert.Equal(7, received.Numbers[^1]);
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

    // Same as RecordingServerChannel for IClientNetChannel SendBulkData overload.
    private sealed class RecordingClientChannel : IClientNetChannel
    {
        public List<INetMessage> Sent { get; } = new();

        public Rex.Shared.Net.ConnectionState State { get; set; }

        public int RoundTripTimeMs => 0;

#pragma warning disable CS0067
        public event Action<INetMessage>? MessageReceived;

        public event Action? Connected;

        public event Action<string>? Disconnected;
#pragma warning restore CS0067

        public void Connect()
        {
        }

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

        public void PollEvents()
        {
        }
    }

    [ProtoContract]
    private sealed class SampleTransferPayload
    {
        [ProtoMember(1)] public string Name { get; set; } = string.Empty;
        [ProtoMember(2)] public List<int> Numbers { get; set; } = new();
    }
}
