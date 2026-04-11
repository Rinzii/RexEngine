using Rex.Shared.Serialization;
using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Common contract for one strongly typed prototype identifier wrapper.
/// </summary>
public interface IPrototypeId
{
    /// <summary>Gets the underlying prototype id string.</summary>
    string Value { get; }
}

/// <summary>Strongly typed entity prototype identifier.</summary>
[NetSerializable]
[Serializable]
public readonly record struct EntityPrototypeId(string Value) : IPrototypeId
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly typed model prototype identifier.</summary>
[NetSerializable]
[Serializable]
public readonly record struct ModelPrototypeId(string Value) : IPrototypeId
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly typed map prototype identifier.</summary>
[NetSerializable]
[Serializable]
public readonly record struct MapPrototypeId(string Value) : IPrototypeId
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly typed scene prototype identifier.</summary>
[NetSerializable]
[Serializable]
public readonly record struct ScenePrototypeId(string Value) : IPrototypeId
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Strongly typed prototype category identifier.</summary>
[NetSerializable]
[Serializable]
public readonly record struct PrototypeCategoryId(string Value) : IPrototypeId
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

[TypeSerializer(typeof(EntityPrototypeId))]
internal sealed class EntityPrototypeIdSerializer : PrototypeIdSerializer<EntityPrototypeId>
{
    protected override EntityPrototypeId Create(string value) => new(value);
}

[TypeSerializer(typeof(ModelPrototypeId))]
internal sealed class ModelPrototypeIdSerializer : PrototypeIdSerializer<ModelPrototypeId>
{
    protected override ModelPrototypeId Create(string value) => new(value);
}

[TypeSerializer(typeof(MapPrototypeId))]
internal sealed class MapPrototypeIdSerializer : PrototypeIdSerializer<MapPrototypeId>
{
    protected override MapPrototypeId Create(string value) => new(value);
}

[TypeSerializer(typeof(ScenePrototypeId))]
internal sealed class ScenePrototypeIdSerializer : PrototypeIdSerializer<ScenePrototypeId>
{
    protected override ScenePrototypeId Create(string value) => new(value);
}

[TypeSerializer(typeof(PrototypeCategoryId))]
internal sealed class PrototypeCategoryIdSerializer : PrototypeIdSerializer<PrototypeCategoryId>
{
    protected override PrototypeCategoryId Create(string value) => new(value);
}

internal abstract class PrototypeIdSerializer<TPrototypeId> : ITypeReader, ITypeWriter, ITypeCopier, ITypeValidator
    where TPrototypeId : struct, IPrototypeId
{
    public object Read(SerializationManager manager, Type type, DataNode node, bool notNullableOverride,
        ISerializationContext? context)
    {
        if (node is not ValueDataNode valueNode || string.IsNullOrWhiteSpace(valueNode.Value))
        {
            throw new InvalidOperationException($"Prototype id '{typeof(TPrototypeId).FullName}' must deserialize from a scalar value.");
        }

        PrototypeValidation.ValidateIdentifier(valueNode.Value, nameof(node));
        return Create(valueNode.Value);
    }

    public DataNode Write(SerializationManager manager, Type type, object? value, bool alwaysWrite,
        ISerializationContext? context)
    {
        return value is null
            ? new ValueDataNode(string.Empty)
            : new ValueDataNode(((TPrototypeId)value).Value);
    }

    public object Copy(SerializationManager manager, Type type, object? source, ISerializationContext? context,
        bool skipHook)
    {
        return source ?? default(TPrototypeId);
    }

    public ValidationNode Validate(SerializationManager manager, Type type, DataNode node, ISerializationContext? context)
    {
        try
        {
            _ = Read(manager, type, node, notNullableOverride: false, context);
            return new ValidationNode(valid: true);
        }
        catch (Exception exception)
        {
            return new ValidationNode(valid: false, exception.Message);
        }
    }

    protected abstract TPrototypeId Create(string value);
}
