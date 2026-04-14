namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a field or property as an include-style data field that reads and writes against the full mapping node.
/// </summary>
public sealed class IncludeDataFieldAttribute : DataFieldBaseAttribute
{
    /// <summary>
    /// Creates an include data field attribute.
    /// </summary>
    /// <param name="tag">Optional serialized field name.</param>
    public IncludeDataFieldAttribute(string? tag = null)
        : base(tag)
    {
    }
}
