namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a field or property as a regular serialized data field.
/// </summary>
public sealed class DataFieldAttribute : DataFieldBaseAttribute
{
    /// <summary>
    /// Creates a regular data field attribute.
    /// </summary>
    /// <param name="tag">Optional serialized field name.</param>
    public DataFieldAttribute(string? tag = null)
        : base(tag)
    {
    }
}
