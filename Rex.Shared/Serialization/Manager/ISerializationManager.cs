namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Reflection-based serialization manager for shared data definitions.
/// </summary>
public interface ISerializationManager
{
    /// <summary>Reads a boxed value from a data node.</summary>
    object? Read(Type type, DataNode node, Func<object?>? instanceProvider = null, bool notNullableOverride = false,
        ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Reads a value from a data node.</summary>
    T Read<T>(DataNode node, Func<T>? instanceProvider = null, bool notNullableOverride = false,
        ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Writes a boxed value to a data node.</summary>
    DataNode WriteValue(Type type, object? value, bool alwaysWrite = false, ISerializationContext? context = null,
        bool skipHook = false);

    /// <summary>Writes a value to a data node.</summary>
    DataNode WriteValue<T>(T value, bool alwaysWrite = false, ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Validates a data node against a boxed target type.</summary>
    ValidationNode Validate(Type type, DataNode node, ISerializationContext? context = null);

    /// <summary>Validates a data node against a target type.</summary>
    ValidationNode Validate<T>(DataNode node, ISerializationContext? context = null);

    /// <summary>Creates a deep copy of a boxed value.</summary>
    object? CreateCopy(Type type, object? source, ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Creates a deep copy of a value.</summary>
    T CreateCopy<T>(T source, ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Copies values from one boxed instance into another compatible instance.</summary>
    void CopyTo(Type type, object source, object target, ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Copies values from one instance into another compatible instance.</summary>
    void CopyTo<T>(T source, T target, ISerializationContext? context = null, bool skipHook = false);

    /// <summary>Composes multiple nodes using one boxed target type definition.</summary>
    DataNode Compose(Type type, params DataNode[] nodes);

    /// <summary>Composes multiple mapping nodes using one target type definition.</summary>
    MappingDataNode Compose<T>(params MappingDataNode[] nodes);
}
