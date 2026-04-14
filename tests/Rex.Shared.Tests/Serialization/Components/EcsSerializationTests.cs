using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Entities;
using Rex.Shared.Entities.World;
using Rex.Shared.Serialization.Components;
using Rex.Shared.Tests.Entities.Support;

namespace Rex.Shared.Tests.Serialization.Components;

public sealed class EcsSerializationTests
{
    [Fact]
    public void Serialize_and_deserialize_round_trip_world_state()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();
        var world = new EcsWorld(registry);
        EntityId entity = world.CreateEntity();

        world.Add(entity, new TransformComponent { X = 1f, Y = 2f, Z = 3f, RotationY = 90f });
        world.Add(entity, new OwnerComponent { OwnerClientId = Guid.NewGuid() });
        world.Add(entity, new HealthComponent { Current = 5, Max = 10 });

        SerializedWorld serialized = world.Serialize();
        var restored = EcsWorld.Deserialize(EcsTestSupport.CreateRegistry(), serialized);

        _ = Assert.Single(serialized.Entities);
        Assert.True(restored.Exists(entity));
        Assert.Equal(world.Get<TransformComponent>(entity).RotationY, restored.Get<TransformComponent>(entity).RotationY);
        Assert.Equal(world.Get<OwnerComponent>(entity).OwnerClientId, restored.Get<OwnerComponent>(entity).OwnerClientId);
        Assert.Equal(world.Get<HealthComponent>(entity).Current, restored.Get<HealthComponent>(entity).Current);
    }

    [Fact]
    public void Serialize_orders_component_payloads_by_stable_component_id()
    {
        var world = new EcsWorld(EcsTestSupport.CreateRegistry());
        EntityId entity = world.CreateEntity();

        world.Add(entity, new OwnerComponent { OwnerClientId = Guid.NewGuid() });
        world.Add(entity, new TransformComponent { X = 7f });
        world.Add(entity, new HealthComponent { Current = 8, Max = 9 });

        SerializedWorld serialized = world.Serialize();
        int[] componentIds = serialized.Entities[0].Components.Select(component => component.Key).ToArray();

        Assert.Equal(
            [SharedEcsBootstrap.TransformComponentId, SharedEcsBootstrap.OwnerComponentId, EcsTestSupport.HealthComponentId],
            componentIds);
    }

    [Fact]
    public void Deserialize_throws_on_unknown_component_id()
    {
        var serialized = new SerializedWorld(
        [
            new SerializedEntity(
                new EntityId(1, 1),
                [new KeyValuePair<int, byte[]>(9999, [1, 2, 3])])
        ]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => EcsWorld.Deserialize(EcsTestSupport.CreateRegistry(), serialized));

        Assert.Contains("unknown component id", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Protobuf_serializer_round_trips_built_in_component_payloads()
    {
        ProtobufComponentSerializer<TransformComponent> serializer = ProtobufComponentSerializer<TransformComponent>.Instance;
        var original = new TransformComponent
        {
            X = 1f,
            Y = 2f,
            Z = 3f,
            RotationY = 45f
        };

        byte[] payload = serializer.Serialize(original);
        TransformComponent restored = serializer.Deserialize(payload);

        Assert.Equal(original.X, restored.X);
        Assert.Equal(original.Y, restored.Y);
        Assert.Equal(original.Z, restored.Z);
        Assert.Equal(original.RotationY, restored.RotationY);
    }
}
