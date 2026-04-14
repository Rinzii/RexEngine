namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Provides per-operation serializer registrations.
/// </summary>
public interface ISerializationContext
{
    /// <summary>Gets the serializer provider for this context.</summary>
    SerializationProvider SerializerProvider { get; }
}
