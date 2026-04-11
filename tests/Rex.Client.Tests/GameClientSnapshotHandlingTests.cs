using LiteNetLib;
using Microsoft.Extensions.Logging.Abstractions;
using Rex.Sandbox.Client.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Replication;
using Rex.Shared.Serialization.Components;
using NetConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Sandbox.Client.Tests;

public sealed class GameClientSnapshotHandlingTests
{
    [Fact]
    public void Delta_without_baseline_requests_full_state_then_acks_full_snapshot()
    {
        GameClient client = new(NullLoggerFactory.Instance);
        RecordingClientChannel channel = new();

        client.Connect(channel);
        channel.Sent.Clear();

        channel.RaiseMessageReceived(new WorldSnapshotMessage(
            1u,
            0u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: false));

        RequestFullStateMessage fullStateRequest = Assert.IsType<RequestFullStateMessage>(Assert.Single(channel.Sent));
        Assert.Equal(0u, fullStateRequest.LastAppliedServerTick);
        Assert.True(client.WorldState.NeedsFullState);

        channel.Sent.Clear();

        channel.RaiseMessageReceived(new WorldSnapshotMessage(
            2u,
            0u,
            [
                SnapshotEntity(1, 2f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        StateAckMessage ack = Assert.IsType<StateAckMessage>(Assert.Single(channel.Sent));
        Assert.Equal(2u, ack.AcknowledgedTick);
        Assert.False(client.WorldState.NeedsFullState);
        EntityState entity = Assert.Single(client.WorldState.CurrentEntities);
        Assert.Equal(2f, entity.X);
    }

    private static ReplicatedEntityState SnapshotEntity(int entityId, float x, float y, float z, float rotationY)
    {
        return new ReplicatedEntityState(
            entityId,
            [
                new ReplicatedComponentState(
                    1000,
                    ProtobufComponentSerializer<TransformComponent>.Instance.Serialize(new TransformComponent
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        RotationY = rotationY
                    }))
            ]);
    }

    private sealed class RecordingClientChannel : IClientNetChannel
    {
        public List<INetMessage> Sent { get; } = [];

        public NetConnectionState State { get; set; }

        public int RoundTripTimeMs => 0;

        public event Action<INetMessage>? MessageReceived;

        public event Action? Connected;

        public event Action<string>? Disconnected;

        public void Connect()
        {
            Connected?.Invoke();
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
            Disconnected?.Invoke(reason);
        }

        public void PollEvents()
        {
        }

        public void RaiseMessageReceived(INetMessage message)
        {
            MessageReceived?.Invoke(message);
        }
    }
}
