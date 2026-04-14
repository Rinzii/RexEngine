using ProtoBuf;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Components.BuiltIn;

/// <summary>
/// Minimal shared transform component for the Phase 1 ECS runtime.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct TransformComponent : IComponent
{
    /// <summary>World-space X position.</summary>
    [ProtoMember(1)]
    [DataField]
    public float X { get; set; }

    /// <summary>World-space Y position.</summary>
    [ProtoMember(2)]
    [DataField]
    public float Y { get; set; }

    /// <summary>World-space Z position.</summary>
    [ProtoMember(3)]
    [DataField]
    public float Z { get; set; }

    /// <summary>Rotation around the Y axis in degrees.</summary>
    [ProtoMember(4)]
    [DataField]
    public float RotationY { get; set; }
}
