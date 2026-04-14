using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Shared authored map prototype that points at one map resource.
/// </summary>
[Prototype]
public sealed partial class MapPrototype : IInheritingPrototype, ISerializationHook
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

    /// <summary>Gets the relative resource path to the map asset.</summary>
    [DataField("map")]
    public string Map { get; set; } = string.Empty;

    /// <summary>Gets the optional scene prototype associated with this map.</summary>
    [DataField("scene")]
    public string? Scene { get; set; }

    /// <summary>Gets optional authored tags attached to the map prototype.</summary>
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
        PrototypeValidation.ValidateRelativePath(Map, nameof(Map));

        if (!Map.EndsWith(".map.json", StringComparison.OrdinalIgnoreCase)
            && !Map.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Map prototype '{Id}' must reference a .map or .map.json resource.");
        }

        if (!string.IsNullOrWhiteSpace(Scene))
        {
            PrototypeValidation.ValidateIdentifier(Scene, nameof(Scene));
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
