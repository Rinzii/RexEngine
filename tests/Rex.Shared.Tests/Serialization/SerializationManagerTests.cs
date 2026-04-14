using Rex.Shared.Serialization.Manager;
using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Tests.Serialization;

[DataDefinition]
public sealed partial class DemoData : ISerializationHook
{
    [DataField]
    public int Count { get; set; }

    [DataField]
    public List<string> Names { get; set; } = [];

    [DataField(CustomTypeSerializer = typeof(UpperStringSerializer))]
    public string Label { get; set; } = string.Empty;

    [IncludeDataField]
    public DemoNestedData Nested { get; set; } = new();

    public bool AfterDeserialized { get; private set; }

    public bool BeforeSerialized { get; private set; }

    public void BeforeSerialization()
    {
        BeforeSerialized = true;
    }

    public void AfterDeserialization()
    {
        AfterDeserialized = true;
    }
}

[DataDefinition]
public sealed partial class DemoNestedData
{
    [DataField]
    public string Extra { get; set; } = string.Empty;
}

[MeansDataDefinition]
[AttributeUsage(AttributeTargets.Class)]
public sealed class DemoPrototypeAttribute : Attribute;

[DemoPrototype]
public sealed partial class MeansDataDefinitionExample
{
    [DataField]
    public string Id { get; set; } = string.Empty;
}

[ImplicitDataDefinitionForInheritors]
public interface IImplicitDefinition
{
}

public sealed partial class ImplicitDefinitionExample : IImplicitDefinition
{
    [DataField]
    public int Value { get; set; }
}

public sealed class UpperStringSerializer : ITypeReader, ITypeWriter
{
    public object? Read(SerializationManager manager, Type type, DataNode node, bool notNullableOverride,
        ISerializationContext? context)
    {
        ValueDataNode scalar = Assert.IsType<ValueDataNode>(node);
        return scalar.Value?.ToUpperInvariant();
    }

    public DataNode Write(SerializationManager manager, Type type, object? value, bool alwaysWrite,
        ISerializationContext? context)
    {
        string? text = value as string;
        return new ValueDataNode(text?.ToLowerInvariant());
    }
}

public sealed class SerializationManagerTests
{
    private readonly SerializationManager _manager = new();

    [Fact]
    public void WriteValue_writes_mapping_and_omits_default_values()
    {
        DemoData data = new()
        {
            Count = 5,
            Names = ["alice", "bob"],
            Label = "LOUD",
            Nested = new DemoNestedData
            {
                Extra = "nested"
            }
        };

        MappingDataNode node = Assert.IsType<MappingDataNode>(_manager.WriteValue(data));

        Assert.True(data.BeforeSerialized);
        Assert.True(node.TryGet("count", out DataNode countNode));
        Assert.Equal("5", Assert.IsType<ValueDataNode>(countNode).Value);
        Assert.True(node.TryGet("label", out DataNode labelNode));
        Assert.Equal("loud", Assert.IsType<ValueDataNode>(labelNode).Value);
        Assert.True(node.TryGet("extra", out DataNode extraNode));
        Assert.Equal("nested", Assert.IsType<ValueDataNode>(extraNode).Value);
    }

    [Fact]
    public void Read_reads_mapping_and_runs_hooks()
    {
        MappingDataNode node = new();
        node.Set("count", new ValueDataNode("7"));
        SequenceDataNode names = new();
        names.Sequence.Add(new ValueDataNode("alice"));
        names.Sequence.Add(new ValueDataNode("bob"));
        node.Set("names", names);
        node.Set("label", new ValueDataNode("quiet"));
        node.Set("extra", new ValueDataNode("nested"));

        DemoData data = _manager.Read<DemoData>(node);

        Assert.True(data.AfterDeserialized);
        Assert.Equal(7, data.Count);
        Assert.Equal(["alice", "bob"], data.Names);
        Assert.Equal("QUIET", data.Label);
        Assert.Equal("nested", data.Nested.Extra);
    }

    [Fact]
    public void CreateCopy_creates_deep_copy()
    {
        DemoData source = new()
        {
            Count = 3,
            Names = ["alpha"],
            Label = "copy",
            Nested = new DemoNestedData
            {
                Extra = "value"
            }
        };

        DemoData copy = _manager.CreateCopy(source, skipHook: true);
        copy.Names.Add("beta");
        copy.Nested.Extra = "changed";

        Assert.Equal(["alpha"], source.Names);
        Assert.Equal("value", source.Nested.Extra);
    }

    [Fact]
    public void Validate_returns_invalid_for_bad_scalar()
    {
        MappingDataNode node = new();
        node.Set("count", new ValueDataNode("NaN"));

        ValidationNode validation = _manager.Validate<DemoData>(node);

        Assert.False(validation.Valid);
    }

    [Fact]
    public void Compose_merges_mapping_nodes()
    {
        MappingDataNode first = new();
        first.Set("count", new ValueDataNode("1"));
        first.Set("extra", new ValueDataNode("base"));

        MappingDataNode second = new();
        second.Set("count", new ValueDataNode("2"));

        MappingDataNode composed = _manager.Compose<DemoData>(first, second);

        Assert.Equal("2", Assert.IsType<ValueDataNode>(Assert.IsType<MappingDataNode>(composed).Values["count"]).Value);
        Assert.Equal("base", Assert.IsType<ValueDataNode>(composed.Values["extra"]).Value);
    }

    [Fact]
    public void Read_supports_means_data_definition_and_implicit_data_definition()
    {
        MappingDataNode meansNode = new();
        meansNode.Set("id", new ValueDataNode("demo"));

        MeansDataDefinitionExample means = _manager.Read<MeansDataDefinitionExample>(meansNode);
        Assert.Equal("demo", means.Id);

        MappingDataNode implicitNode = new();
        implicitNode.Set("value", new ValueDataNode("42"));

        ImplicitDefinitionExample implicitValue = _manager.Read<ImplicitDefinitionExample>(implicitNode);
        Assert.Equal(42, implicitValue.Value);
    }
}
