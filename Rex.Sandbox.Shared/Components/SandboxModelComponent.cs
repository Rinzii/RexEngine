using ProtoBuf;
using Rex.Shared.Components;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Sandbox.Shared.Components;

/// <summary>
/// Authored model prototype selection for sandbox entities.
/// </summary>
[ProtoContract]
[DataDefinition]
public partial struct SandboxModelComponent : IComponent
{
    /// <summary>Resolved shared model prototype id.</summary>
    [ProtoMember(1)]
    [DataField]
    public string ModelId { get; set; }
}
