using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Shared scene prototype that groups map selection and authored entity placements.
/// </summary>
[Prototype]
public sealed partial class ScenePrototype : IInheritingPrototype, ISerializationHook
{
    /// <inheritdoc />
    [DataField("id")]
    public string Id { get; set; } = string.Empty;

    /// <inheritdoc />
    [DataField("abstract")]
    public bool Abstract { get; set; }

    /// <inheritdoc />
    [DataField("parent")]
    public string? Parent { get; set; }

    IReadOnlyList<string>? IInheritingPrototype.Parents => GetParents();

    /// <summary>Gets the optional user-facing name.</summary>
    [DataField("name")]
    public string? Name { get; set; }

    /// <summary>Gets the optional user-facing description.</summary>
    [DataField("description")]
    public string? Description { get; set; }

    /// <summary>Gets the optional map prototype used by this scene.</summary>
    [DataField("map")]
    public string? Map { get; set; }

    /// <summary>Gets the authored entity placements for this scene.</summary>
    [DataField("entities")]
    public List<SceneEntityPlacement> Entities { get; set; } = [];

    /// <summary>Gets optional authored tags attached to the scene prototype.</summary>
    [DataField("tags")]
    public string[] Tags { get; set; } = [];

    /// <summary>Gets the optional additional ordered parent ids.</summary>
    [DataField("parents")]
    public string[]? AdditionalParents { get; set; }

    /// <inheritdoc />
    public void BeforeSerialization()
    {
    }

    /// <inheritdoc />
    public void AfterDeserialization()
    {
        PrototypeValidation.ValidateIdentifier(Id, nameof(Id));

        if (!string.IsNullOrWhiteSpace(Map))
        {
            PrototypeValidation.ValidateIdentifier(Map, nameof(Map));
        }

        foreach (SceneEntityPlacement entity in Entities)
        {
            entity.Validate();
        }

        foreach (string tag in Tags)
        {
            PrototypeValidation.ValidateIdentifier(tag, nameof(Tags));
        }
    }

    private string[]? GetParents()
    {
        if (!string.IsNullOrWhiteSpace(Parent) && AdditionalParents is { Length: > 0 })
        {
            return [Parent, .. AdditionalParents];
        }

        if (AdditionalParents is { Length: > 0 })
        {
            return AdditionalParents;
        }

        if (!string.IsNullOrWhiteSpace(Parent))
        {
            return [Parent];
        }

        return null;
    }
}

/// <summary>
/// One authored entity placement inside a scene prototype.
/// </summary>
[DataDefinition]
public sealed partial class SceneEntityPlacement
{
    /// <summary>Gets the entity prototype to spawn for this placement.</summary>
    [DataField("prototype")]
    public string Prototype { get; set; } = string.Empty;

    /// <summary>Gets the optional user-facing placement name.</summary>
    [DataField("name")]
    public string? Name { get; set; }

    /// <summary>Gets the X position of the placement.</summary>
    [DataField("x")]
    public float X { get; set; }

    /// <summary>Gets the Y position of the placement.</summary>
    [DataField("y")]
    public float Y { get; set; }

    /// <summary>Gets the Z position of the placement.</summary>
    [DataField("z")]
    public float Z { get; set; }

    /// <summary>Gets the Y-axis rotation of the placement in degrees.</summary>
    [DataField("rotationY")]
    public float RotationY { get; set; }

    internal void Validate()
    {
        PrototypeValidation.ValidateIdentifier(Prototype, nameof(Prototype));
    }
}
