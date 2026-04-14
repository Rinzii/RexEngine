using ProtoBuf;
using Rex.Shared.Components;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Sandbox.Shared.Components;

/// <summary>
/// Sandbox movement tuning stored as gameplay data on moving entities.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct SandboxMoverComponent : IComponent
{
    /// <summary>Planar movement distance applied per sampled input tick.</summary>
    [ProtoMember(1)]
    [DataField]
    public float PlanarUnitsPerInputTick { get; set; }
}
