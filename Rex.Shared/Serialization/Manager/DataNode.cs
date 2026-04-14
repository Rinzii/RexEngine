namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Base node type for reflected serialization data.
/// </summary>
public abstract class DataNode
{
    /// <summary>
    /// Creates a deep copy of the node.
    /// </summary>
    /// <returns>Cloned node tree.</returns>
    public abstract DataNode Clone();
}

/// <summary>
/// Scalar node storing one textual value.
/// </summary>
public sealed class ValueDataNode : DataNode
{
    /// <summary>
    /// Creates a scalar node.
    /// </summary>
    /// <param name="value">Scalar value.</param>
    public ValueDataNode(string? value)
    {
        Value = value;
    }

    /// <summary>Gets the scalar value.</summary>
    public string? Value { get; }

    /// <inheritdoc />
    public override DataNode Clone() => new ValueDataNode(Value);
}

/// <summary>
/// Sequence node storing ordered child nodes.
/// </summary>
public sealed class SequenceDataNode : DataNode
{
    /// <summary>Gets the ordered child nodes.</summary>
    public List<DataNode> Sequence { get; } = [];

    /// <inheritdoc />
    public override DataNode Clone()
    {
        SequenceDataNode clone = new();
        foreach (DataNode node in Sequence)
        {
            clone.Sequence.Add(node.Clone());
        }

        return clone;
    }
}

/// <summary>
/// Mapping node storing named child nodes.
/// </summary>
public sealed class MappingDataNode : DataNode
{
    private readonly Dictionary<string, DataNode> _values = new(StringComparer.Ordinal);

    /// <summary>Gets the mapped values.</summary>
    public IReadOnlyDictionary<string, DataNode> Values => _values;

    /// <summary>
    /// Adds or replaces a mapping entry.
    /// </summary>
    /// <param name="key">Mapping key.</param>
    /// <param name="node">Node value.</param>
    public void Set(string key, DataNode node)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(node);
        _values[key] = node;
    }

    /// <summary>
    /// Attempts to resolve a mapping entry.
    /// </summary>
    /// <param name="key">Mapping key.</param>
    /// <param name="node">Resolved node.</param>
    /// <returns><see langword="true"/> when the key exists.</returns>
    public bool TryGet(string key, out DataNode node)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _values.TryGetValue(key, out node!);
    }

    /// <inheritdoc />
    public override DataNode Clone()
    {
        MappingDataNode clone = new();
        foreach ((string key, DataNode value) in _values)
        {
            clone.Set(key, value.Clone());
        }

        return clone;
    }
}
