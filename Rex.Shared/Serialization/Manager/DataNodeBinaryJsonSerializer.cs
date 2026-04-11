using System.Globalization;
using System.Text;

namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Reads and writes a compact binary JSON-style encoding for <see cref="DataNode"/> trees.
/// </summary>
public static class DataNodeBinaryJsonSerializer
{
    private const uint Magic = 0x314A4252; // RBJ1
    private const byte CurrentVersion = 1;

    /// <summary>
    /// Parses a binary JSON payload into a data node tree.
    /// </summary>
    /// <param name="payload">Binary JSON payload.</param>
    /// <returns>Parsed data node tree.</returns>
    public static DataNode Read(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            throw new ArgumentException("Binary JSON payload must not be empty.", nameof(payload));
        }

        using MemoryStream stream = new(payload.ToArray(), writable: false);
        using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);

        uint magic = reader.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidOperationException("Binary JSON payload has an invalid magic header.");
        }

        byte version = reader.ReadByte();
        if (version != CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Binary JSON version {version} is not supported. Expected version {CurrentVersion}.");
        }

        DataNode node = ReadNode(reader);
        if (stream.Position != stream.Length)
        {
            throw new InvalidOperationException("Binary JSON payload contains trailing data.");
        }

        return node;
    }

    /// <summary>
    /// Writes a data node tree into a compact binary JSON payload.
    /// </summary>
    /// <param name="node">Node tree to write.</param>
    /// <returns>Binary JSON payload.</returns>
    public static byte[] Write(DataNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            WriteNode(writer, node);
        }

        return stream.ToArray();
    }

    private static DataNode ReadNode(BinaryReader reader)
    {
        var token = (BinaryJsonToken)reader.ReadByte();
        switch (token)
        {
            case BinaryJsonToken.Object:
                MappingDataNode mapping = new();
                int propertyCount = reader.ReadInt32();
                for (int i = 0; i < propertyCount; i++)
                {
                    string key = reader.ReadString();
                    mapping.Set(key, ReadNode(reader));
                }

                return mapping;

            case BinaryJsonToken.Array:
                SequenceDataNode sequence = new();
                int itemCount = reader.ReadInt32();
                for (int i = 0; i < itemCount; i++)
                {
                    sequence.Sequence.Add(ReadNode(reader));
                }

                return sequence;

            case BinaryJsonToken.String:
                return new ValueDataNode(reader.ReadString());

            case BinaryJsonToken.Int64:
                return new ValueDataNode(reader.ReadInt64().ToString(CultureInfo.InvariantCulture));

            case BinaryJsonToken.Decimal:
                return new ValueDataNode(reader.ReadDecimal().ToString(CultureInfo.InvariantCulture));

            case BinaryJsonToken.True:
                return new ValueDataNode(bool.TrueString);

            case BinaryJsonToken.False:
                return new ValueDataNode(bool.FalseString);

            case BinaryJsonToken.Null:
                return new ValueDataNode(null);

            default:
                throw new InvalidOperationException($"Binary JSON token '{token}' is not supported.");
        }
    }

    private static void WriteNode(BinaryWriter writer, DataNode node)
    {
        switch (node)
        {
            case MappingDataNode mappingNode:
                writer.Write((byte)BinaryJsonToken.Object);
                writer.Write(mappingNode.Values.Count);
                foreach ((string key, DataNode child) in mappingNode.Values)
                {
                    writer.Write(key);
                    WriteNode(writer, child);
                }

                return;

            case SequenceDataNode sequenceNode:
                writer.Write((byte)BinaryJsonToken.Array);
                writer.Write(sequenceNode.Sequence.Count);
                foreach (DataNode child in sequenceNode.Sequence)
                {
                    WriteNode(writer, child);
                }

                return;

            case ValueDataNode valueNode:
                WriteValue(writer, valueNode.Value);
                return;

            default:
                throw new InvalidOperationException(
                    $"Unsupported data node type '{node.GetType().FullName}'.");
        }
    }

    private static void WriteValue(BinaryWriter writer, string? value)
    {
        if (value == null)
        {
            writer.Write((byte)BinaryJsonToken.Null);
            return;
        }

        if (bool.TryParse(value, out bool boolValue))
        {
            writer.Write((byte)(boolValue ? BinaryJsonToken.True : BinaryJsonToken.False));
            return;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
        {
            writer.Write((byte)BinaryJsonToken.Int64);
            writer.Write(longValue);
            return;
        }

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
        {
            writer.Write((byte)BinaryJsonToken.Decimal);
            writer.Write(decimalValue);
            return;
        }

        writer.Write((byte)BinaryJsonToken.String);
        writer.Write(value);
    }

    private enum BinaryJsonToken : byte
    {
        Object = 1,
        Array = 2,
        String = 3,
        Int64 = 4,
        Decimal = 5,
        True = 6,
        False = 7,
        Null = 8
    }
}
