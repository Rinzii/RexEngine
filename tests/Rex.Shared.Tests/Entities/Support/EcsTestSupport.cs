using ProtoBuf;
using Rex.Shared.Components;
using Rex.Shared.Components.Registration;
using Rex.Shared.Serialization.Components;

namespace Rex.Shared.Tests.Entities.Support;

internal static class EcsTestSupport
{
    internal const int VelocityComponentId = 2000;
    internal const int HealthComponentId = 2001;

    internal static ComponentRegistry CreateRegistry(bool includeTestComponents = true)
    {
        var registry = new ComponentRegistry();
        SharedEcsBootstrap.RegisterAll(registry);

        if (includeTestComponents)
        {
            registry.Register(VelocityComponentId, "velocity", ProtobufComponentSerializer<VelocityComponent>.Instance);
            registry.Register(HealthComponentId, "health", ProtobufComponentSerializer<HealthComponent>.Instance);
        }

        return registry;
    }
}

[ProtoContract]
internal struct VelocityComponent : IComponent
{
    [ProtoMember(1)] public float X { get; set; }
    [ProtoMember(2)] public float Y { get; set; }
}

[ProtoContract]
internal struct HealthComponent : IComponent
{
    [ProtoMember(1)] public int Current { get; set; }
    [ProtoMember(2)] public int Max { get; set; }
}

[ProtoContract]
internal struct TestTagComponent : IComponent
{
}
