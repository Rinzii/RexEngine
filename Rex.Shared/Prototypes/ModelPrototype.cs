using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Shared model prototype definition that points at one authored RDM package.
/// </summary>
[Prototype]
public sealed partial class ModelPrototype : IInheritingPrototype, ISerializationHook
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

    /// <inheritdoc />
    IReadOnlyList<string>? IInheritingPrototype.Parents => GetParents();

    /// <summary>Gets the optional user-facing name.</summary>
    [DataField("name")]
    public string? Name { get; set; }

    /// <summary>Gets the optional user-facing description.</summary>
    [DataField("description")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the relative resource path to the authored RDM metadata file.</summary>
    [DataField("rdm")]
    public string Rdm { get; set; } = string.Empty;

    /// <summary>Gets or sets the optional package-local RDM prototype to use by default.</summary>
    [DataField("packagePrototype")]
    public string? PackagePrototype { get; set; }

    /// <summary>Gets or sets the optional default RDM state override.</summary>
    [DataField("defaultState")]
    public string? DefaultState { get; set; }

    /// <summary>Gets or sets optional free-form model metadata.</summary>
    [DataField("tags")]
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.Ordinal);

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
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        PrototypeValidation.ValidateRelativePath(Rdm, nameof(Rdm));

        if (!Rdm.EndsWith(".rdm", StringComparison.OrdinalIgnoreCase)
            && !Rdm.EndsWith(".rdmb", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Model prototype '{Id}' must reference an .rdm or .rdmb metadata file.");
        }

        if (!string.IsNullOrWhiteSpace(PackagePrototype))
        {
            PrototypeValidation.ValidateIdentifier(PackagePrototype, nameof(PackagePrototype));
        }

        if (!string.IsNullOrWhiteSpace(DefaultState))
        {
            PrototypeValidation.ValidateIdentifier(DefaultState, nameof(DefaultState));
        }

        foreach ((string key, string value) in Tags)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
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
