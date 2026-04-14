using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.Storage;
using Rex.Shared.Entities.World;
using Rex.Shared.Tests.Entities.Support;

namespace Rex.Shared.Tests.Entities.World;

public sealed class EcsLifecycleTests
{
    [Fact]
    public void CreateEntity_uses_empty_archetype_and_reuses_slots_with_new_generations()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());

        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();

        Assert.Equal(1, first.Slot);
        Assert.Equal(1, first.Generation);
        Assert.Equal(2, second.Slot);
        Assert.Equal(1, second.Generation);
        Assert.Equal(2, world.Count);
        Assert.Equal(1, world.ArchetypeCount);

        Assert.True(world.DestroyEntity(first));
        EntityId recycled = world.CreateEntity();

        Assert.Equal(first.Slot, recycled.Slot);
        Assert.Equal(first.Generation + 1, recycled.Generation);
        Assert.False(world.Exists(first));
        Assert.True(world.Exists(recycled));
    }

    [Fact]
    public void DestroyEntity_updates_moved_entity_record_after_swap_remove()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId first = world.CreateEntity();
        EntityId second = world.CreateEntity();

        world.Add(first, new TransformComponent { X = 1f, Z = 2f });
        world.Add(second, new TransformComponent { X = 3f, Z = 4f });

        Assert.True(world.DestroyEntity(first));

        TransformComponent remaining = world.Get<TransformComponent>(second);
        Assert.Equal(3f, remaining.X);
        Assert.Equal(4f, remaining.Z);
        Assert.True(world.Exists(second));
    }

    [Fact]
    public void Exists_returns_false_for_invalid_or_stale_handles()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();

        Assert.False(world.Exists(EntityId.Invalid));

        Assert.True(world.DestroyEntity(entity));
        Assert.False(world.Exists(entity));
    }

    [Fact]
    public void Signature_canonicalizes_component_ids_deterministically()
    {
        var signatureA = ArchetypeSignature.FromComponentIds(
            [SharedEcsBootstrap.OwnerComponentId, SharedEcsBootstrap.TransformComponentId, EcsTestSupport.HealthComponentId]);
        var signatureB = ArchetypeSignature.FromComponentIds(
            [EcsTestSupport.HealthComponentId, SharedEcsBootstrap.TransformComponentId, SharedEcsBootstrap.OwnerComponentId]);

        Assert.Equal(signatureA, signatureB);
        Assert.True(signatureA.Contains(SharedEcsBootstrap.TransformComponentId));
        Assert.Equal(
            [SharedEcsBootstrap.TransformComponentId, SharedEcsBootstrap.OwnerComponentId, EcsTestSupport.HealthComponentId],
            signatureA.ToArray());
    }

    [Fact]
    public void Component_versions_advance_for_add_set_ref_and_singleton_updates()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();

        world.Add(entity, new TransformComponent { X = 1f });
        uint addVersion = world.GetComponentVersion<TransformComponent>(entity);

        world.Set(entity, new TransformComponent { X = 2f });
        uint setVersion = world.GetComponentVersion<TransformComponent>(entity);

        ref TransformComponent transform = ref world.GetMutableRef<TransformComponent>(entity);
        transform.X = 3f;
        uint refVersion = world.GetComponentVersion<TransformComponent>(entity);

        world.AddSingleton(new OwnerComponent { OwnerClientId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") });
        uint singletonAddVersion = world.GetSingletonVersion<OwnerComponent>();
        ref OwnerComponent owner = ref world.GetMutableSingletonRef<OwnerComponent>();
        owner.OwnerClientId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        uint singletonRefVersion = world.GetSingletonVersion<OwnerComponent>();
        world.SetSingleton(new OwnerComponent { OwnerClientId = Guid.Parse("ffffffff-1111-2222-3333-444444444444") });
        uint singletonSetVersion = world.GetSingletonVersion<OwnerComponent>();

        Assert.True(addVersion > 0);
        Assert.True(setVersion > addVersion);
        Assert.True(refVersion > setVersion);
        Assert.True(singletonAddVersion > refVersion);
        Assert.True(singletonRefVersion > singletonAddVersion);
        Assert.True(singletonSetVersion > singletonRefVersion);
    }
}
