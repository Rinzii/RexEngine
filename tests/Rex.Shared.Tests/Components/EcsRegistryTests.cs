using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.Serialization.Components;
using Rex.Shared.Tests.Entities.Support;

namespace Rex.Shared.Tests.Components;

public sealed class EcsRegistryTests
{
    [Fact]
    public void Register_rejects_duplicate_id_name_and_type()
    {
        var registry = new ComponentRegistry();
        registry.Register(1, "transform", ProtobufComponentSerializer<TransformComponent>.Instance);

        _ = Assert.Throws<InvalidOperationException>(
            () => registry.Register(1, "other-transform", ProtobufComponentSerializer<OwnerComponent>.Instance));
        _ = Assert.Throws<InvalidOperationException>(
            () => registry.Register(2, "transform", ProtobufComponentSerializer<OwnerComponent>.Instance));
        _ = Assert.Throws<InvalidOperationException>(
            () => registry.Register(3, "transform-duplicate-type", ProtobufComponentSerializer<TransformComponent>.Instance));
    }

    [Fact]
    public void Freeze_prevents_future_registration()
    {
        var registry = new ComponentRegistry();
        registry.Freeze();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => registry.Register(1, "transform", ProtobufComponentSerializer<TransformComponent>.Instance));

        Assert.Contains("frozen", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Lookup_by_id_and_type_uses_registered_identity()
    {
        ComponentRegistry registry = EcsTestSupport.CreateRegistry();

        Assert.True(registry.IsRegistered<TransformComponent>());
        Assert.True(registry.IsRegistered<MetaDataComponent>());
        Assert.True(registry.TryGetComponentId(typeof(OwnerComponent), out int ownerId));
        Assert.Equal(SharedEcsBootstrap.TransformComponentId, registry.GetComponentId<TransformComponent>());
        Assert.Equal(SharedEcsBootstrap.MetaDataComponentId, registry.GetComponentId<MetaDataComponent>());
        Assert.Equal("owner", registry.GetComponentName<OwnerComponent>());
        Assert.Equal(typeof(OwnerComponent), registry.GetComponentType(ownerId));
    }

    [Fact]
    public void SharedEcsBootstrap_registers_builtins_idempotently()
    {
        var registry = new ComponentRegistry();

        SharedEcsBootstrap.RegisterAll(registry);
        SharedEcsBootstrap.RegisterAll(registry);

        Assert.Equal(3, registry.Count);
        Assert.Equal(SharedEcsBootstrap.TransformComponentId, registry.GetComponentId<TransformComponent>());
        Assert.Equal(SharedEcsBootstrap.OwnerComponentId, registry.GetComponentId<OwnerComponent>());
        Assert.Equal(SharedEcsBootstrap.MetaDataComponentId, registry.GetComponentId<MetaDataComponent>());
    }
}
