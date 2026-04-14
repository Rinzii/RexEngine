using Rex.Sandbox.Shared.Components;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.Entities.Queries;
using Rex.Shared.Net.Replication;
using Rex.Shared.Simulation;

namespace Rex.Sandbox.Shared.Tests.Simulation;

// Minimal authoritative Sandbox world spawn input and snapshots.
public sealed class GameWorldTests
{
    [Fact]
    public void SpawnEntity_returns_monotonic_ids_and_tracks_position()
    {
        GameWorld world = new();
        string typeName = "TestMob";
        int a = world.SpawnEntity(Guid.Empty, typeName, 1f, 2f, 3f);
        int b = world.SpawnEntity(Guid.Empty, typeName, 4f, 5f, 6f);

        Assert.Equal(1, a);
        Assert.Equal(2, b);
        Assert.Equal(2, world.Entities.Count);
        Assert.Equal(1f, world.Entities[a].X);
        Assert.Equal(6f, world.Entities[b].Z);
        Assert.Equal(2, world.EntityManager.World.Count);
    }

    [Fact]
    public void SpawnEntity_player_uses_shared_prototype_components()
    {
        GameWorld world = new();
        var ownerClientId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        int entityId = world.SpawnEntity(ownerClientId, EntityTypeIds.Player, 1f, 2f, 3f);

        ComponentQueryEnumerator<SandboxActorComponent, SandboxModelComponent, OwnerComponent> query =
            world.EntityManager.World.Query<SandboxActorComponent, SandboxModelComponent, OwnerComponent>().GetEnumerator();

        bool found = false;
        while (query.MoveNext())
        {
            ref readonly SandboxActorComponent actor = ref query.Component1;
            if (actor.NetEntityId != entityId)
            {
                continue;
            }

            ref readonly SandboxModelComponent model = ref query.Component2;
            ref readonly OwnerComponent owner = ref query.Component3;

            Assert.Equal(EntityTypeIds.Player, actor.EntityType);
            Assert.Equal(SandboxPrototypeIds.Player, actor.PrototypeId);
            Assert.Equal("mannyRefModel", model.ModelId);
            Assert.Equal(ownerClientId, owner.OwnerClientId);
            found = true;
            break;
        }

        Assert.True(found);
    }

    [Fact]
    public void DestroyEntity_removes_entry()
    {
        GameWorld world = new();
        int id = world.SpawnEntity(Guid.Empty, "X", 0f, 0f, 0f);
        world.DestroyEntity(id);
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void ProcessInput_moves_entity_on_xz_and_sets_rotation_from_look_y()
    {
        GameWorld world = new();
        int id = world.SpawnEntity(Guid.Empty, "Pawn", 0f, 0f, 0f);
        PlayerInputMessage input = new(1u, 1f, 0f, 0f, 90f, 0u);
        world.ProcessInput(id, input);

        EntityState e = world.Entities[id];
        Assert.Equal(5f, e.X);
        Assert.Equal(0f, e.Z);
        Assert.Equal(90f, e.RotationY);
    }

    [Fact]
    public void ProcessInput_unknown_entity_is_noop()
    {
        GameWorld world = new();
        world.ProcessInput(999, new PlayerInputMessage(0u, 1f, 1f, 0f, 0f, 0u));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Tick_increments_CurrentTick()
    {
        GameWorld world = new();
        world.Tick(0.016f);
        world.Tick(0.016f);
        Assert.Equal(2u, world.CurrentTick);
    }

    [Fact]
    public void BuildSnapshot_contains_all_entities_BuildDelta_only_dirty()
    {
        DirtyTracker tracker = new(256);
        GameWorld world = new(tracker);
        int id1 = world.SpawnEntity(Guid.Empty, "E", 0f, 0f, 0f);
        _ = world.SpawnEntity(Guid.Empty, "E", 1f, 1f, 1f);

        WorldSnapshotMessage full = world.BuildSnapshot(10u, 9u);
        Assert.True(full.IsFullSnapshot);
        Assert.Equal(2, full.Entities.Count);

        WorldSnapshotMessage delta = world.BuildDeltaSnapshot(10u, 9u, [id1], []);
        Assert.False(delta.IsFullSnapshot);
        _ = Assert.Single(delta.Entities);
        Assert.Equal(id1, delta.Entities[0].EntityId);
        Assert.Empty(delta.RemovedKeys);
    }

    [Fact]
    public void BuildDeltaSnapshot_includes_removed_entity_ids()
    {
        DirtyTracker tracker = new(256);
        GameWorld world = new(tracker);
        int id = world.SpawnEntity(Guid.Empty, "E", 0f, 0f, 0f);
        world.DestroyEntity(id);

        WorldSnapshotMessage delta = world.BuildDeltaSnapshot(10u, 9u, [], [id]);

        Assert.False(delta.IsFullSnapshot);
        Assert.Empty(delta.Entities);
        Assert.Equal([id], delta.RemovedKeys);
    }

    [Fact]
    public void Tick_records_external_ecs_transform_changes_in_dirty_history_for_multiple_ack_windows()
    {
        DirtyTracker tracker = new(256);
        GameWorld world = new(tracker);
        int id = world.SpawnEntity(Guid.Empty, "E", 0f, 0f, 0f);
        world.Tick(0.016f);

        ComponentQueryEnumerator<SandboxActorComponent, TransformComponent> query =
            world.EntityManager.World.Query<SandboxActorComponent, TransformComponent>().GetEnumerator();
        Assert.True(query.MoveNext());
        ref TransformComponent transform = ref query.MutableComponent2;
        transform.X = 42f;
        _ = world.Entities;

        world.Tick(0.016f);
        HashSet<int>? dirtyFromRecentAck = tracker.GetDirtyEntities(1u, world.CurrentTick);
        HashSet<int>? dirtyFromInitialAck = tracker.GetDirtyEntities(0u, world.CurrentTick);
        Assert.NotNull(dirtyFromRecentAck);
        Assert.NotNull(dirtyFromInitialAck);

        WorldSnapshotMessage recentDelta = world.BuildDeltaSnapshot(world.CurrentTick, 0u, dirtyFromRecentAck!, []);
        WorldSnapshotMessage initialDelta = world.BuildDeltaSnapshot(world.CurrentTick, 0u, dirtyFromInitialAck!, []);

        ReplicatedEntityState recentEntity = Assert.Single(recentDelta.Entities);
        ReplicatedEntityState initialEntity = Assert.Single(initialDelta.Entities);
        Assert.Equal(id, recentEntity.EntityId);
        Assert.Equal(id, initialEntity.EntityId);
    }

    [Fact]
    public void Tick_records_external_ecs_entity_removals_in_dirty_history_for_multiple_ack_windows()
    {
        DirtyTracker tracker = new(256);
        GameWorld world = new(tracker);
        int id = world.SpawnEntity(Guid.Empty, "E", 0f, 0f, 0f);
        world.Tick(0.016f);

        ComponentQueryEnumerator<SandboxActorComponent> query =
            world.EntityManager.World.Query<SandboxActorComponent>().GetEnumerator();
        Assert.True(query.MoveNext());
        EntityId entity = query.Entity;
        Assert.True(world.EntityManager.DeleteEntity(entity));
        _ = world.Entities;

        world.Tick(0.016f);
        HashSet<int>? removedFromRecentAck = tracker.GetRemovedEntities(1u, world.CurrentTick);
        HashSet<int>? removedFromInitialAck = tracker.GetRemovedEntities(0u, world.CurrentTick);
        Assert.NotNull(removedFromRecentAck);
        Assert.NotNull(removedFromInitialAck);

        WorldSnapshotMessage recentDelta = world.BuildDeltaSnapshot(world.CurrentTick, 0u, [], removedFromRecentAck!);
        WorldSnapshotMessage initialDelta = world.BuildDeltaSnapshot(world.CurrentTick, 0u, [], removedFromInitialAck!);

        Assert.Empty(recentDelta.Entities);
        Assert.Empty(initialDelta.Entities);
        Assert.Equal([id], recentDelta.RemovedKeys);
        Assert.Equal([id], initialDelta.RemovedKeys);
    }
}
