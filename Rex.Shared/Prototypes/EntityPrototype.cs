using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Shared entity prototype definition for ECS-backed spawning.
/// </summary>
[Prototype]
public sealed partial class EntityPrototype : IInheritingPrototype
{
    /// <inheritdoc />
    [DataField("id")]
    public string Id { get; set; } = string.Empty;

    /// <inheritdoc />
    [DataField("abstract")]
    public bool Abstract { get; set; }

    /// <inheritdoc />
    [DataField("parent")]
    public string? Parent { get; set; }

    /// <inheritdoc />
    IReadOnlyList<string>? IInheritingPrototype.Parents => GetParents();

    /// <summary>Gets the optional user-facing name.</summary>
    [DataField("name")]
    public string? Name { get; set; }

    /// <summary>Gets the optional user-facing description.</summary>
    [DataField("description")]
    public string? Description { get; set; }

    /// <summary>Gets the component definitions to attach when this prototype is spawned.</summary>
    [DataField("components", CustomTypeSerializer = typeof(EntityPrototypeComponentMapSerializer))]
    public Dictionary<string, MappingDataNode> Components { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Gets the optional additional ordered parent ids.</summary>
    [DataField("parents")]
    public string[]? AdditionalParents { get; set; }

    private string[]? GetParents()
    {
        if (!string.IsNullOrWhiteSpace(Parent) && AdditionalParents is { Length: > 0 })
        {
            return [Parent, .. AdditionalParents];
        }

        if (AdditionalParents is { Length: > 0 })
        {
            return AdditionalParents;
        }

        if (!string.IsNullOrWhiteSpace(Parent))
        {
            return [Parent];
        }

        return null;
    }
}

[TypeSerializer(typeof(Dictionary<string, MappingDataNode>))]
internal sealed class EntityPrototypeComponentMapSerializer : ITypeReader, ITypeWriter, ITypeCopier, ITypeValidator
{
    public object Read(SerializationManager manager, Type type, DataNode node, bool notNullableOverride,
        ISerializationContext? context)
    {
        if (node is not MappingDataNode mapping)
        {
            throw new InvalidOperationException("Entity prototype components must deserialize from a mapping node.");
        }

        Dictionary<string, MappingDataNode> values = new(StringComparer.Ordinal);
        foreach ((string key, DataNode child) in mapping.Values)
        {
            if (child is not MappingDataNode componentMapping)
            {
                throw new InvalidOperationException(
                    $"Entity prototype component '{key}' must deserialize from a mapping node.");
            }

            values.Add(key, (MappingDataNode)componentMapping.Clone());
        }

        return values;
    }

    public DataNode Write(SerializationManager manager, Type type, object? value, bool alwaysWrite,
        ISerializationContext? context)
    {
        MappingDataNode mapping = new();
        if (value is null)
        {
            return mapping;
        }

        foreach ((string key, MappingDataNode componentMapping) in (Dictionary<string, MappingDataNode>)value)
        {
            mapping.Set(key, componentMapping.Clone());
        }

        return mapping;
    }

    public object? Copy(SerializationManager manager, Type type, object? source, ISerializationContext? context,
        bool skipHook)
    {
        if (source == null)
        {
            return null;
        }

        var sourceValues = (Dictionary<string, MappingDataNode>)source;
        Dictionary<string, MappingDataNode> copy = new(StringComparer.Ordinal);
        foreach ((string key, MappingDataNode componentMapping) in sourceValues)
        {
            copy.Add(key, (MappingDataNode)componentMapping.Clone());
        }

        return copy;
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
}
