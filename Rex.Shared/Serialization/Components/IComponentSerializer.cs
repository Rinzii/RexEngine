using Rex.Shared.Components;
using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Serialization.Components;

/// <summary>
/// Serializes one ECS component type to and from a compact payload.
/// </summary>
public interface IComponentSerializer<T>
    where T : struct, IComponent
{
    /// <summary>Serializes one component instance into a payload.</summary>
    /// <param name="component">Component instance to serialize.</param>
    /// <returns>Serialized component payload.</returns>
    byte[] Serialize(T component);

    /// <summary>Deserializes one component instance from a payload.</summary>
    /// <param name="payload">Serialized component payload.</param>
    /// <returns>Deserialized component instance.</returns>
    T Deserialize(ReadOnlyMemory<byte> payload);
}

/// <summary>
/// Protobuf-net serializer for the core ECS components defined in this library.
/// </summary>
public sealed class ProtobufComponentSerializer<T> : IComponentSerializer<T>
    where T : struct, IComponent
{
    /// <summary>Shared singleton instance for the component type.</summary>
    public static ProtobufComponentSerializer<T> Instance { get; } = new();

    /// <inheritdoc />
    public byte[] Serialize(T component) => ProtoSerializer.Serialize(component);

    /// <inheritdoc />
    public T Deserialize(ReadOnlyMemory<byte> payload) => ProtoSerializer.Deserialize<T>(payload);
}
