using ProtoBuf;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Protobuf-net serialize/deserialize helpers.
/// </summary>
public static class ProtoSerializer
{
    /// <summary>Serializes using protobuf-net attributes on <typeparamref name="T"/>.</summary>
    public static byte[] Serialize<T>(T value)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, value);
        return ms.ToArray();
    }

    /// <summary>Deserializes from a full protobuf payload (no Rex message header).</summary>
    /// <param name="data">Raw protobuf bytes.</param>
    public static T Deserialize<T>(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return Serializer.Deserialize<T>(ms);
    }

    /// <summary>Same as array overload. Copies to a stream because protobuf-net wants a stream.</summary>
    public static T Deserialize<T>(ReadOnlyMemory<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray());
        return Serializer.Deserialize<T>(ms);
    }
}