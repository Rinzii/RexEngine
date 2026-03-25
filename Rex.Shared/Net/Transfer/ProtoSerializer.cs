using ProtoBuf;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Protobuf-net serialize/deserialize helpers.
/// </summary>
public static class ProtoSerializer
{
    /// <summary>
    /// Serializes a value into a byte array.
    /// </summary>
    public static byte[] Serialize<T>(T value)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, value);
        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes a value from a byte array.
    /// </summary>
    public static T Deserialize<T>(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return Serializer.Deserialize<T>(ms);
    }

    /// <summary>
    /// Deserializes a value from a memory block.
    /// </summary>
    public static T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray());
        return Serializer.Deserialize<T>(ms);
    }
}
