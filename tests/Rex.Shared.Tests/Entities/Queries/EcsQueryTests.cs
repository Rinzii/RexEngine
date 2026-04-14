using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.Entities.Queries;
using Rex.Shared.Entities.World;
using Rex.Shared.Tests.Entities.Support;

namespace Rex.Shared.Tests.Entities.Queries;

public sealed class EcsQueryTests
{
    [Fact]
    public void Query_matches_required_and_excluded_components()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId transformOnly = world.CreateEntity();
        EntityId transformAndVelocity = world.CreateEntity();
        EntityId velocityOnly = world.CreateEntity();

        world.Add(transformOnly, new TransformComponent { X = 1f });
        world.Add(transformAndVelocity, new TransformComponent { X = 2f });
        world.Add(transformAndVelocity, new VelocityComponent { X = 3f });
        world.Add(velocityOnly, new VelocityComponent { X = 4f });

        var entities = new List<EntityId>();
        ComponentQueryEnumerator<TransformComponent> enumerator = world.Query<TransformComponent>(new QueryDescription(excludedTypes: [typeof(VelocityComponent)])).GetEnumerator();
        while (enumerator.MoveNext())
        {
            entities.Add(enumerator.Entity);
        }

        Assert.Equal([transformOnly], entities);
    }

    [Fact]
    public void Query_cache_refreshes_when_new_archetypes_are_created()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();
        ComponentQuery<TransformComponent> query = world.Query<TransformComponent>();

        ComponentQueryEnumerator<TransformComponent> firstEnumerator = query.GetEnumerator();
        Assert.False(firstEnumerator.MoveNext());
        Assert.Equal(1, world.QueryCacheCount);

        world.Add(entity, new TransformComponent { X = 10f });

        ComponentQueryEnumerator<TransformComponent> secondEnumerator = query.GetEnumerator();
        Assert.True(secondEnumerator.MoveNext());
        Assert.Equal(entity, secondEnumerator.Entity);
    }

    [Fact]
    public void Query_iteration_exposes_direct_component_refs_for_mutation()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();
        world.Add(entity, new TransformComponent { X = 10f });
        world.Add(entity, new VelocityComponent { X = 2f });
        uint beforeRefVersion = world.GetComponentVersion<TransformComponent>(entity);

        ComponentQueryEnumerator<TransformComponent, VelocityComponent> enumerator = world.Query<TransformComponent, VelocityComponent>().GetEnumerator();
        Assert.True(enumerator.MoveNext());

        ref TransformComponent transform = ref enumerator.MutableComponent1;
        ref readonly VelocityComponent velocity = ref enumerator.Component2;
        transform.X += velocity.X;

        Assert.Equal(12f, world.Get<TransformComponent>(entity).X);
        Assert.True(world.GetComponentVersion<TransformComponent>(entity) > beforeRefVersion);
    }

    [Fact]
    public void Query_iteration_is_allocation_free_after_cache_warmup()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        for (int i = 0; i < 64; i++)
        {
            EntityId entity = world.CreateEntity();
            world.Add(entity, new TransformComponent { X = i });
        }

        ComponentQuery<TransformComponent> query = world.Query<TransformComponent>();
        ComponentQueryEnumerator<TransformComponent> warmEnumerator = query.GetEnumerator();
        while (warmEnumerator.MoveNext())
        {
            _ = warmEnumerator.Component1.X;
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 10; iteration++)
        {
            ComponentQueryEnumerator<TransformComponent> enumerator = query.GetEnumerator();
            while (enumerator.MoveNext())
            {
                _ = enumerator.Entity;
                _ = enumerator.Component1.X;
            }
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0L, after - before);
    }

    [Fact]
    public void Query_iteration_visits_entities_across_multiple_chunks()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        for (int i = 0; i < 130; i++)
        {
            EntityId entity = world.CreateEntity();
            world.Add(entity, new TransformComponent { X = i });
        }

        int visited = 0;
        ComponentQueryEnumerator<TransformComponent> enumerator = world.Query<TransformComponent>().GetEnumerator();
        while (enumerator.MoveNext())
        {
            visited++;
        }

        Assert.Equal(130, visited);
    }

    [Fact]
    public void Query_iteration_throws_when_world_changes_structurally()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();
        world.Add(entity, new TransformComponent { X = 1f });

        ComponentQueryEnumerator<TransformComponent> enumerator = world.Query<TransformComponent>().GetEnumerator();
        Assert.True(enumerator.MoveNext());

        world.Add(entity, new VelocityComponent { X = 2f });

        try
        {
            _ = enumerator.MoveNext();
            throw new Xunit.Sdk.XunitException("Expected query iteration to fail after a structural world change.");
        }
        catch (InvalidOperationException exception)
        {
            Assert.Contains("changed structurally", exception.Message, StringComparison.Ordinal);
        }
    }
}
