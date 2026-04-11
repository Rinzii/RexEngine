using LiteNetLib.Utils;

namespace Rex.Shared.Net;

/// <summary>Fixed 16-byte GUID layout for LiteNetLib payloads. Matches <see cref="Guid"/> wire order.</summary>
public static class NetGuidExtensions
{
    /// <summary>Writes a GUID as 16 bytes without a length prefix.</summary>
    public static void PutGuid(this NetDataWriter writer, Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        _ = value.TryWriteBytes(bytes);
        for (int i = 0; i < 16; i++)
        {
            writer.Put(bytes[i]);
        }
    }

    /// <summary>Reads 16 bytes as a GUID.</summary>
    public static Guid ReadGuid(this NetDataReader reader)
    {
        Span<byte> bytes = stackalloc byte[16];
        for (int i = 0; i < 16; i++)
        {
            bytes[i] = reader.GetByte();
        }

        return new Guid(bytes);
    }
}
