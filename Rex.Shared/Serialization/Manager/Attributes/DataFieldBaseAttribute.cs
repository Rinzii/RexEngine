namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Base attribute for a reflected data field.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public abstract class DataFieldBaseAttribute : Attribute
{
    /// <summary>
    /// Creates a data field attribute.
    /// </summary>
    /// <param name="tag">Optional serialized field name.</param>
    protected DataFieldBaseAttribute(string? tag = null)
    {
        Tag = tag;
    }

    /// <summary>Gets the optional serialized field name.</summary>
    public string? Tag { get; }

    /// <summary>Gets or sets an optional custom type serializer type for the field.</summary>
    public Type? CustomTypeSerializer { get; set; }
}
