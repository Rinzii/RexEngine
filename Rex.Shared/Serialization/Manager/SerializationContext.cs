namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Default mutable serialization context.
/// </summary>
public sealed class SerializationContext : ISerializationContext
{
    /// <summary>Gets the serializer provider for this context.</summary>
    public SerializationProvider SerializerProvider { get; } = new();
}
