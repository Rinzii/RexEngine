namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Reads one type from a data node.
/// </summary>
public interface ITypeReader
{
    /// <summary>Reads one value from a data node.</summary>
    object? Read(SerializationManager manager, Type type, DataNode node, bool notNullableOverride,
        ISerializationContext? context);
}

/// <summary>
/// Writes one type to a data node.
/// </summary>
public interface ITypeWriter
{
    /// <summary>Writes one value to a data node.</summary>
    DataNode Write(SerializationManager manager, Type type, object? value, bool alwaysWrite, ISerializationContext? context);
}

/// <summary>
/// Validates one type against a data node.
/// </summary>
public interface ITypeValidator
{
    /// <summary>Validates one value against a data node.</summary>
    ValidationNode Validate(SerializationManager manager, Type type, DataNode node, ISerializationContext? context);
}

/// <summary>
/// Creates a deep copy of one type.
/// </summary>
public interface ITypeCopier
{
    /// <summary>Creates a deep copy of one value.</summary>
    object? Copy(SerializationManager manager, Type type, object? source, ISerializationContext? context, bool skipHook);
}

/// <summary>
/// Composes multiple data nodes for one type.
/// </summary>
public interface ITypeComposer
{
    /// <summary>Composes multiple nodes for one type.</summary>
    DataNode Compose(SerializationManager manager, Type type, IReadOnlyList<DataNode> nodes, ISerializationContext? context);
}
