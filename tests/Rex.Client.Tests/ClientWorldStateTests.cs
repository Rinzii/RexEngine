using Rex.Sandbox.Client.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.GameStates;
using Rex.Shared.Net.Replication;
using Rex.Shared.Serialization.Components;

namespace Rex.Sandbox.Client.Tests;

public sealed class ClientWorldStateTests
{
    [Fact]
    public void ApplySnapshot_merges_partial_updates_into_a_full_render_state()
    {
        ClientWorldState state = new();
        WorldSnapshotMessage first = new(
            1u,
            0u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f),
                SnapshotEntity(2, 10f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true);
        WorldSnapshotMessage second = new(
            2u,
            1u,
            [
                SnapshotEntity(1, 2f, 0f, 0f, 0f)
            ],
            isFullSnapshot: false);

        Assert.Equal(GameStateApplyResult.Applied, state.ApplySnapshot(first));
        Assert.Equal(GameStateApplyResult.Applied, state.ApplySnapshot(second));
        Assert.False(state.NeedsFullState);

        Assert.Collection(
            state.CurrentEntities,
            entity =>
            {
                Assert.Equal(1, entity.EntityId);
                Assert.Equal(2f, entity.X);
            },
            entity =>
            {
                Assert.Equal(2, entity.EntityId);
                Assert.Equal(10f, entity.X);
            });
    }

    [Fact]
    public void ApplySpawn_and_destroy_update_the_current_world_state()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(1u, 0u, [SnapshotEntity(1, 1f, 0f, 0f, 0f)], true));

        state.ApplySpawn(new EntitySpawnMessage(2u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));
        state.ApplyDestroy(new EntityDestroyMessage(2u, 1));

        EntityState remaining = Assert.Single(state.CurrentEntities);
        Assert.Equal(2, remaining.EntityId);
        Assert.Equal(5f, remaining.X);
        Assert.Equal(7f, remaining.Z);
        Assert.Equal(90f, remaining.RotationY);
    }

    [Fact]
    public void ApplySnapshot_delta_removed_keys_retire_entities_without_waiting_for_destroy_message()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            1u,
            0u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f),
                SnapshotEntity(2, 2f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        GameStateApplyResult result = state.ApplySnapshot(new WorldSnapshotMessage(
            2u,
            1u,
            [],
            isFullSnapshot: false,
            removedEntityIds: [1]));

        Assert.Equal(GameStateApplyResult.Applied, result);
        EntityState remaining = Assert.Single(state.CurrentEntities);
        Assert.Equal(2, remaining.EntityId);
    }

    [Fact]
    public void ApplySpawn_preserves_components_from_existing_snapshot_state_on_newer_side_channel_tick()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            1u,
            0u,
            [
                new ReplicatedEntityState(
                    2,
                    [
                        TransformPayload(5f, 0f, 7f, 90f),
                        OwnerPayload(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"))
                    ])
            ],
            isFullSnapshot: true));

        state.ApplySpawn(new EntitySpawnMessage(2u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));

        ReplicatedEntityState current = Assert.Single(state.CurrentSnapshot!.Entities);
        Assert.Equal(3, current.Components.Count);
        Assert.Contains(current.Components, static component => component.ComponentId == SharedEcsBootstrap.OwnerComponentId);
    }

    [Fact]
    public void ApplySpawn_and_destroy_ignore_same_tick_entity_events_after_snapshot()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            5u,
            4u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        state.ApplySpawn(new EntitySpawnMessage(5u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));
        state.ApplyDestroy(new EntityDestroyMessage(5u, 1));

        EntityState remaining = Assert.Single(state.CurrentEntities);
        Assert.Equal(1, remaining.EntityId);
    }

    [Fact]
    public void ApplySpawn_buffers_future_entity_events_across_intermediate_snapshots()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            5u,
            4u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        state.ApplySpawn(new EntitySpawnMessage(7u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            6u,
            5u,
            [
                SnapshotEntity(1, 2f, 0f, 0f, 0f)
            ],
            isFullSnapshot: false));

        Assert.Collection(
            state.CurrentEntities,
            entity =>
            {
                Assert.Equal(1, entity.EntityId);
                Assert.Equal(2f, entity.X);
            },
            entity =>
            {
                Assert.Equal(2, entity.EntityId);
                Assert.Equal(5f, entity.X);
                Assert.Equal(7f, entity.Z);
                Assert.Equal(90f, entity.RotationY);
            });
        Assert.Equal(6u, state.LastServerTick);
    }

    [Fact]
    public void ApplySnapshot_discards_buffered_future_entity_events_when_snapshot_catches_up()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            5u,
            4u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        state.ApplySpawn(new EntitySpawnMessage(7u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            7u,
            6u,
            [
                SnapshotEntity(1, 3f, 0f, 0f, 0f)
            ],
            isFullSnapshot: false));

        EntityState remaining = Assert.Single(state.CurrentEntities);
        Assert.Equal(1, remaining.EntityId);
        Assert.Equal(3f, remaining.X);
    }

    [Fact]
    public void ApplySpawn_and_destroy_ignore_stale_entity_events()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            5u,
            4u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        state.ApplySpawn(new EntitySpawnMessage(4u, 2, Guid.Empty, "player", 5f, 0f, 7f, 90f));
        state.ApplyDestroy(new EntityDestroyMessage(4u, 1));

        EntityState remaining = Assert.Single(state.CurrentEntities);
        Assert.Equal(1, remaining.EntityId);
    }

    [Fact]
    public void ApplySnapshot_ignores_stale_frames()
    {
        ClientWorldState state = new();
        WorldSnapshotMessage current = new(3u, 2u, [SnapshotEntity(1, 3f, 0f, 0f, 0f)], true);
        WorldSnapshotMessage stale = new(2u, 1u, [SnapshotEntity(1, 2f, 0f, 0f, 0f)], false);

        Assert.Equal(GameStateApplyResult.Applied, state.ApplySnapshot(current));
        Assert.Equal(GameStateApplyResult.IgnoredStale, state.ApplySnapshot(stale));

        EntityState entity = Assert.Single(state.CurrentEntities);
        Assert.Equal(3f, entity.X);
    }

    [Fact]
    public void ApplySnapshot_full_snapshot_replaces_stale_entities()
    {
        ClientWorldState state = new();
        _ = state.ApplySnapshot(new WorldSnapshotMessage(
            1u,
            0u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f),
                SnapshotEntity(2, 2f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        GameStateApplyResult applied = state.ApplySnapshot(new WorldSnapshotMessage(
            2u,
            1u,
            [
                SnapshotEntity(1, 10f, 0f, 0f, 0f)
            ],
            isFullSnapshot: true));

        Assert.Equal(GameStateApplyResult.Applied, applied);
        EntityState entity = Assert.Single(state.CurrentEntities);
        Assert.Equal(1, entity.EntityId);
        Assert.Equal(10f, entity.X);
    }

    [Fact]
    public void ApplySnapshot_delta_without_baseline_requests_full_state()
    {
        ClientWorldState state = new();

        GameStateApplyResult applied = state.ApplySnapshot(new WorldSnapshotMessage(
            1u,
            0u,
            [
                SnapshotEntity(1, 1f, 0f, 0f, 0f)
            ],
            isFullSnapshot: false));

        Assert.Equal(GameStateApplyResult.MissingBaseline, applied);
        Assert.True(state.NeedsFullState);
        Assert.Empty(state.CurrentEntities);
    }

    private static ReplicatedEntityState SnapshotEntity(int entityId, float x, float y, float z, float rotationY)
    {
        return new ReplicatedEntityState(
            entityId,
            [
                TransformPayload(x, y, z, rotationY)
            ]);
    }

    private static ReplicatedComponentState TransformPayload(float x, float y, float z, float rotationY)
    {
        return new ReplicatedComponentState(
            SharedEcsBootstrap.TransformComponentId,
            ProtobufComponentSerializer<TransformComponent>.Instance.Serialize(new TransformComponent
            {
                X = x,
                Y = y,
                Z = z,
                RotationY = rotationY
            }));
    }

    private static ReplicatedComponentState OwnerPayload(Guid ownerClientId)
    {
        return new ReplicatedComponentState(
            SharedEcsBootstrap.OwnerComponentId,
            ProtobufComponentSerializer<OwnerComponent>.Instance.Serialize(new OwnerComponent
            {
                OwnerClientId = ownerClientId
            }));
    }
}
