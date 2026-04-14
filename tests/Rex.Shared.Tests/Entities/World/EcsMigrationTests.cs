using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.Tests.Entities.Support;

namespace Rex.Shared.Tests.Entities.World;

public sealed class EcsMigrationTests
{
    [Fact]
    public void Add_and_remove_preserve_component_data_across_archetype_moves()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();

        world.Add(entity, new TransformComponent { X = 12f, Y = 2f, Z = -5f, RotationY = 45f });
        world.Add(entity, new VelocityComponent { X = 4f, Y = -1f });
        world.Add(entity, new HealthComponent { Current = 20, Max = 30 });

        Assert.Equal(4, world.ArchetypeCount);

        Assert.True(world.Remove<VelocityComponent>(entity));

        TransformComponent transform = world.Get<TransformComponent>(entity);
        HealthComponent health = world.Get<HealthComponent>(entity);

        Assert.Equal(12f, transform.X);
        Assert.Equal(-5f, transform.Z);
        Assert.Equal(45f, transform.RotationY);
        Assert.Equal(20, health.Current);
        Assert.False(world.Has<VelocityComponent>(entity));
    }

    [Fact]
    public void Adding_components_in_different_orders_reuses_the_same_destination_archetype()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();

        world.Add(first, new TransformComponent { X = 1f });
        world.Add(first, new VelocityComponent { X = 2f });

        world.Add(second, new VelocityComponent { X = 3f });
        world.Add(second, new TransformComponent { X = 4f });

        Assert.Equal(4, world.ArchetypeCount);
        Assert.True(world.Has<TransformComponent>(first));
        Assert.True(world.Has<VelocityComponent>(first));
        Assert.True(world.Has<TransformComponent>(second));
        Assert.True(world.Has<VelocityComponent>(second));
    }

    [Fact]
    public void Remove_updates_swapped_entity_record_inside_the_source_archetype()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();

        world.Add(first, new TransformComponent { X = 5f });
        world.Add(second, new TransformComponent { X = 9f });

        Assert.True(world.Remove<TransformComponent>(first));

        TransformComponent secondTransform = world.Get<TransformComponent>(second);
        Assert.Equal(9f, secondTransform.X);
        Assert.True(world.Exists(second));
    }
}
