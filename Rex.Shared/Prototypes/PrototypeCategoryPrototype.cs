using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Shared prototype grouping used to categorize authored definitions by type.
/// </summary>
[Prototype]
public sealed partial class PrototypeCategoryPrototype : IInheritingPrototype, ISerializationHook
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

    /// <summary>Gets the prototype kind that every entry in this category must belong to.</summary>
    [DataField("prototypeType")]
    public string PrototypeType { get; set; } = string.Empty;

    /// <summary>Gets the ordered prototype ids included in this category.</summary>
    [DataField("entries")]
    public string[] Entries { get; set; } = [];

    /// <summary>Gets optional authored tags attached to the category.</summary>
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
        PrototypeValidation.ValidateIdentifier(PrototypeType, nameof(PrototypeType));

        foreach (string entry in Entries)
        {
            PrototypeValidation.ValidateIdentifier(entry, nameof(Entries));
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
