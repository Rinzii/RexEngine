using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Reads and writes <see cref="DataNode"/> trees from a JSON text representation.
/// </summary>
public static class DataNodeJsonSerializer
{
    /// <summary>
    /// Parses a JSON string into a data node tree.
    /// </summary>
    /// <param name="json">JSON document to parse.</param>
    /// <returns>Parsed data node tree.</returns>
    public static DataNode Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        return ReadElement(document.RootElement);
    }

    /// <summary>
    /// Parses a JSON file into a data node tree.
    /// </summary>
    /// <param name="path">JSON file to parse.</param>
    /// <returns>Parsed data node tree.</returns>
    public static DataNode ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Read(File.ReadAllText(path));
    }

    /// <summary>
    /// Writes a data node tree into a JSON string.
    /// </summary>
    /// <param name="node">Node tree to write.</param>
    /// <param name="indented">Whether to indent the JSON output.</param>
    /// <returns>JSON representation of the node tree.</returns>
    public static string Write(DataNode node, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(node);

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = indented }))
        {
            WriteNode(writer, node);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static DataNode ReadElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                MappingDataNode mapping = new();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    mapping.Set(property.Name, ReadElement(property.Value));
                }

                return mapping;

            case JsonValueKind.Array:
                SequenceDataNode sequence = new();
                foreach (JsonElement child in element.EnumerateArray())
                {
                    sequence.Sequence.Add(ReadElement(child));
                }

                return sequence;

            case JsonValueKind.String:
                return new ValueDataNode(element.GetString());

            case JsonValueKind.Number:
                return new ValueDataNode(element.GetRawText());

            case JsonValueKind.True:
                return new ValueDataNode(bool.TrueString);

            case JsonValueKind.False:
                return new ValueDataNode(bool.FalseString);

            case JsonValueKind.Null:
                return new ValueDataNode(null);

            default:
                throw new InvalidOperationException(
                    $"JSON token kind '{element.ValueKind}' is not supported for data nodes.");
        }
    }

    private static void WriteNode(Utf8JsonWriter writer, DataNode node)
    {
        switch (node)
        {
            case ValueDataNode valueNode:
                WriteValue(writer, valueNode.Value);
                break;

            case SequenceDataNode sequenceNode:
                writer.WriteStartArray();
                foreach (DataNode child in sequenceNode.Sequence)
                {
                    WriteNode(writer, child);
                }

                writer.WriteEndArray();
                break;

            case MappingDataNode mappingNode:
                writer.WriteStartObject();
                foreach ((string key, DataNode child) in mappingNode.Values)
                {
                    writer.WritePropertyName(key);
                    WriteNode(writer, child);
                }

                writer.WriteEndObject();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported data node type '{node.GetType().FullName}'.");
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, string? value)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        if (bool.TryParse(value, out bool boolValue))
        {
            writer.WriteBooleanValue(boolValue);
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
        {
            writer.WriteNumberValue(decimalValue);
            return;
        }

        writer.WriteStringValue(value);
    }
}
