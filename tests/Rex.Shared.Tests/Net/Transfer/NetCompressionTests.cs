using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// Brotli compress path and size threshold behavior.
public sealed class NetCompressionTests
{
    [Fact]
    // Small payloads skip compression and return the same array reference.
    public void Compress_below_threshold_returns_original_and_not_compressed()
    {
        byte[] data = new byte[NetCompression.CompressionThreshold - 1];
        Array.Fill(data, (byte)7);

        (byte[] outData, bool isCompressed) = NetCompression.Compress(data);

        Assert.False(isCompressed);
        Assert.Same(data, outData);
    }

    [Fact]
    // Larger repeating data compresses then decompresses to the original bytes.
    public void Compress_decompress_round_trip_for_compressible_payload()
    {
        int originalLength = NetCompression.CompressionThreshold + 512;
        byte[] data = new byte[originalLength];
        Array.Fill(data, (byte)9);

        (byte[] compressed, bool isCompressed) = NetCompression.Compress(data);

        Assert.True(isCompressed);
        Assert.True(compressed.Length < data.Length);

        byte[] restored = NetCompression.Decompress(compressed, originalLength);

        Assert.Equal(data, restored);
    }

    [Fact]
    // Finds a payload where Brotli does not shrink so the same array is returned.
    public void Compress_keeps_original_when_brotli_output_is_not_smaller()
    {
        byte[]? candidate = null;
        for (int seed = 0; seed < 4096 && candidate == null; seed++)
        {
            byte[] buf = new byte[384];
            new Random(seed).NextBytes(buf);
            (byte[] outBytes, bool isCompressed) = NetCompression.Compress(buf);
            if (!isCompressed && ReferenceEquals(buf, outBytes))
            {
                candidate = buf;
            }
        }

        Assert.NotNull(candidate);
    }
}
