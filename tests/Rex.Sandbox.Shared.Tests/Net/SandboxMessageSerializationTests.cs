using LiteNetLib.Utils;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Shared.Net;

namespace Rex.Sandbox.Shared.Tests.Net;

// Wire format round trips through NetMessageRegistry for Sandbox protocol messages.
public sealed class SandboxMessageSerializationTests
{
    public SandboxMessageSerializationTests()
    {
        SandboxNetTestBootstrap.EnsureRegistered();
    }

    [Fact]
    public void RoundTrip_PlayerInputMessage()
    {
        PlayerInputMessage original = new(99, 0.25f, -1f, 3.14f, 0.5f, 0xABCD);
        PlayerInputMessage typed = Assert.IsType<PlayerInputMessage>(RoundTrip(original));
        Assert.Equal(original.Tick, typed.Tick);
        Assert.Equal(original.MoveX, typed.MoveX);
        Assert.Equal(original.MoveY, typed.MoveY);
        Assert.Equal(original.LookX, typed.LookX);
        Assert.Equal(original.LookY, typed.LookY);
        Assert.Equal(original.ActionFlags, typed.ActionFlags);
    }

    [Fact]
    public void RoundTrip_ConnectResponse_accepted_and_rejected()
    {
        var clientId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
        ConnectResponseMessage accepted = new(true, clientId, 60, 7, null);
        ConnectResponseMessage decodedOk = Assert.IsType<ConnectResponseMessage>(RoundTrip(accepted));
        Assert.True(decodedOk.Accepted);
        Assert.Equal(clientId, decodedOk.ClientId);
        Assert.Equal(60, decodedOk.TickRate);
        Assert.Equal(7, decodedOk.LocalPlayerEntityId);
        Assert.Null(decodedOk.RejectReason);

        ConnectResponseMessage rejected = new(false, Guid.Empty, 0, 0, "full");
        ConnectResponseMessage decodedNo = Assert.IsType<ConnectResponseMessage>(RoundTrip(rejected));
        Assert.False(decodedNo.Accepted);
        Assert.Equal("full", decodedNo.RejectReason);
    }

    [Fact]
    public void RoundTrip_EntitySpawnMessage()
    {
        var owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
        EntitySpawnMessage original = new(42, owner, "TestEntity", 1f, 2f, 3f);
        EntitySpawnMessage typed = Assert.IsType<EntitySpawnMessage>(RoundTrip(original));
        Assert.Equal(original.EntityId, typed.EntityId);
        Assert.Equal(original.OwnerClientId, typed.OwnerClientId);
        Assert.Equal(original.EntityType, typed.EntityType);
        Assert.Equal(original.X, typed.X);
        Assert.Equal(original.Y, typed.Y);
        Assert.Equal(original.Z, typed.Z);
    }

    [Fact]
    public void RoundTrip_EntityDestroyMessage()
    {
        EntityDestroyMessage original = new(9001);
        EntityDestroyMessage typed = Assert.IsType<EntityDestroyMessage>(RoundTrip(original));
        Assert.Equal(original.EntityId, typed.EntityId);
    }

    [Fact]
    public void RoundTrip_WorldSnapshotMessage_empty_and_multiple_entities()
    {
        WorldSnapshotMessage empty = new(10u, 9u, []);
        WorldSnapshotMessage decodedEmpty = Assert.IsType<WorldSnapshotMessage>(RoundTrip(empty));
        Assert.Equal(10u, decodedEmpty.ServerTick);
        Assert.Equal(9u, decodedEmpty.LastProcessedInputTick);
        Assert.Empty(decodedEmpty.Entities);

        var entities = new EntityState[]
        {
            new(1, 0f, 0f, 0f, 0f),
            new(2, 10f, 20f, 30f, 45f)
        };
        WorldSnapshotMessage multi = new(100u, 99u, entities);
        WorldSnapshotMessage decodedMulti = Assert.IsType<WorldSnapshotMessage>(RoundTrip(multi));
        Assert.Equal(2, decodedMulti.Entities.Count);
        Assert.Equal(1, decodedMulti.Entities[0].EntityId);
        Assert.Equal(45f, decodedMulti.Entities[1].RotationY);
    }

    private static INetMessage RoundTrip(INetMessage original)
    {
        var writer = new NetDataWriter();
        original.Serialize(writer);
        var reader = new NetDataReader();
        reader.SetSource(writer.Data, 0, writer.Length);
        return NetMessageRegistry.Deserialize(reader);
    }
}
