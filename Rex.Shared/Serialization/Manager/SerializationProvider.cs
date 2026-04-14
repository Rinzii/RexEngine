namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Registers serializers for a serialization context or manager.
/// </summary>
public sealed class SerializationProvider
{
    private readonly Dictionary<Type, object> _serializers = [];

    /// <summary>
    /// Registers a serializer for a target type.
    /// </summary>
    /// <param name="targetType">Target type handled by the serializer.</param>
    /// <param name="serializer">Serializer instance.</param>
    public void Register(Type targetType, object serializer)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(serializer);
        _serializers[targetType] = serializer;
    }

    /// <summary>
    /// Registers a serializer for a target type.
    /// </summary>
    /// <typeparam name="T">Target type handled by the serializer.</typeparam>
    /// <param name="serializer">Serializer instance.</param>
    public void Register<T>(object serializer)
    {
        Register(typeof(T), serializer);
    }

    /// <summary>
    /// Attempts to resolve a serializer for a target type.
    /// </summary>
    /// <param name="targetType">Target type.</param>
    /// <param name="serializer">Resolved serializer.</param>
    /// <returns><see langword="true"/> when a serializer exists.</returns>
    public bool TryGet(Type targetType, out object serializer)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        return _serializers.TryGetValue(targetType, out serializer!);
    }
}
