using ProtoBuf;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Components.BuiltIn;


/// <summary>
/// Minimal shared ownership component for authoritative and replicated entity identity.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct OwnerComponent : IComponent
{
    /// <summary>Owning client id for replicated ownership flows.</summary>
    [ProtoMember(1)]
    [DataField]
    public Guid OwnerClientId { get; set; }
}
