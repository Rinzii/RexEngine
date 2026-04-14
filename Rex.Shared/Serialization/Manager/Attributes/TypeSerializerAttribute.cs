namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a serializer type as the default serializer for a specific target type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TypeSerializerAttribute : Attribute
{
    /// <summary>
    /// Creates a type serializer attribute.
    /// </summary>
    /// <param name="targetType">Type handled by the serializer.</param>
    public TypeSerializerAttribute(Type targetType)
    {
        TargetType = targetType;
    }

    /// <summary>Gets the target type handled by the serializer.</summary>
    public Type TargetType { get; }
}
