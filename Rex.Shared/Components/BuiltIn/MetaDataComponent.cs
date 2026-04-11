using ProtoBuf;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Components.BuiltIn;

/// <summary>
/// Gameplay-facing entity metadata tracked alongside the baseline transform component.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct MetaDataComponent : IComponent
{
    /// <summary>Stable authored prototype id when this entity came from one prototype.</summary>
    [ProtoMember(1)]
    [DataField]
    public string? PrototypeId { get; set; }

    /// <summary>User-facing entity name.</summary>
    [ProtoMember(2)]
    [DataField]
    public string EntityName { get; set; }

    /// <summary>Optional user-facing entity description.</summary>
    [ProtoMember(3)]
    [DataField]
    public string? EntityDescription { get; set; }
}
