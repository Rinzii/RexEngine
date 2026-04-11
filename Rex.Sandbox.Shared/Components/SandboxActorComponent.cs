using ProtoBuf;
using Rex.Shared.Components;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Sandbox.Shared.Components;

/// <summary>
/// Shared sandbox actor metadata carried by authored and runtime-spawned entities.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct SandboxActorComponent : IComponent
{
    /// <summary>Stable sandbox-facing network entity id.</summary>
    [ProtoMember(1)]
    [DataField]
    public int NetEntityId { get; set; }

    /// <summary>Stable sandbox entity type id used by the sample protocol.</summary>
    [ProtoMember(2)]
    [DataField]
    public string EntityType { get; set; }

    /// <summary>Optional authored prototype id that created this entity.</summary>
    [ProtoMember(3)]
    [DataField]
    public string? PrototypeId { get; set; }
}
