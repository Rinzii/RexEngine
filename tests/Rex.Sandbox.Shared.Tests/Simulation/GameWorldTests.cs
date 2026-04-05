using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Simulation;

namespace Rex.Sandbox.Shared.Tests.Simulation;

// Minimal authoritative Sandbox world spawn input and snapshots.
public sealed class GameWorldTests
{
    [Fact]
    public void SpawnEntity_returns_monotonic_ids_and_tracks_position()
    {
        var world = new GameWorld();
        var typeName = "TestMob";
        var a = world.SpawnEntity(Guid.Empty, typeName, 1f, 2f, 3f);
        var b = world.SpawnEntity(Guid.Empty, typeName, 4f, 5f, 6f);

        Assert.Equal(1, a);
        Assert.Equal(2, b);
        Assert.Equal(2, world.Entities.Count);
        Assert.Equal(1f, world.Entities[a].X);
        Assert.Equal(6f, world.Entities[b].Z);
    }

    [Fact]
    public void DestroyEntity_removes_entry()
    {
        var world = new GameWorld();
        var id = world.SpawnEntity(Guid.Empty, "X", 0f, 0f, 0f);
        world.DestroyEntity(id);
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void ProcessInput_moves_entity_on_xz_and_sets_rotation_from_look_y()
    {
        var world = new GameWorld();
        var id = world.SpawnEntity(Guid.Empty, "Pawn", 0f, 0f, 0f);
        var input = new PlayerInputMessage(1u, 1f, 0f, 0f, 90f, 0u);
        world.ProcessInput(id, input);

        var e = world.Entities[id];
        Assert.Equal(5f, e.X);
        Assert.Equal(0f, e.Z);
        Assert.Equal(90f, e.RotationY);
    }

    [Fact]
    public void ProcessInput_unknown_entity_is_noop()
    {
        var world = new GameWorld();
        world.ProcessInput(999, new PlayerInputMessage(0u, 1f, 1f, 0f, 0f, 0u));
        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Tick_increments_CurrentTick()
    {
        var world = new GameWorld();
        world.Tick(0.016f);
        world.Tick(0.016f);
        Assert.Equal(2u, world.CurrentTick);
    }

    [Fact]
    public void BuildSnapshot_contains_all_entities_BuildDelta_only_dirty()
    {
        var tracker = new DirtyTracker(256);
        var world = new GameWorld(tracker);
        var id1 = world.SpawnEntity(Guid.Empty, "E", 0f, 0f, 0f);
        _ = world.SpawnEntity(Guid.Empty, "E", 1f, 1f, 1f);

        var full = world.BuildSnapshot(10u, 9u);
        Assert.Equal(2, full.Entities.Count);

        var delta = world.BuildDeltaSnapshot(10u, 9u, new HashSet<int> { id1 });
        Assert.Single(delta.Entities);
        Assert.Equal(id1, delta.Entities[0].EntityId);
    }
}
