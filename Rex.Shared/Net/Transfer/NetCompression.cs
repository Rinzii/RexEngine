using System.IO.Compression;

namespace Rex.Shared.Net.Transfer;

/// <summary>
/// Brotli compression for bulk transfer payloads.
/// </summary>
public static class NetCompression
{
    /// <summary>
    /// Payloads below this size stay uncompressed.
    /// </summary>
    public const int CompressionThreshold = 256;

    /// <summary>
    /// Compresses a payload when that produces a smaller result.
    /// </summary>
    public static (byte[] Data, bool IsCompressed) Compress(byte[] data)
    {
        if (data.Length < CompressionThreshold)
            return (data, false);

        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, true))
        {
            brotli.Write(data);
        }

        var compressed = output.ToArray();

        // Brotli adds overhead. Keep original if we did not shrink.
        if (compressed.Length >= data.Length)
            return (data, false);

        return (compressed, true);
    }

    /// <summary>
    /// Decompresses a payload back to its original size.
    /// </summary>
    public static byte[] Decompress(byte[] compressedData, int originalLength)
    {
        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        var result = new byte[originalLength];
        var totalRead = 0;
        // BrotliStream may not fill the buffer in one read.
        while (totalRead < originalLength)
        {
            var read = brotli.Read(result, totalRead, originalLength - totalRead);
            if (read == 0)
                break;
            totalRead += read;
        }

        return result;
    }
}