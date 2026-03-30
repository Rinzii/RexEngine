using Rex.Shared.Net.Transfer;

namespace Rex.Shared.Tests.Net.Transfer;

// Brotli compress path and size threshold behavior.
public sealed class NetCompressionTests
{
    [Fact]
    // Small payloads skip compression and return the same array reference.
    public void Compress_below_threshold_returns_original_and_not_compressed()
    {
        var data = new byte[NetCompression.CompressionThreshold - 1];
        Array.Fill(data, (byte)7);

        var (outData, isCompressed) = NetCompression.Compress(data);

        Assert.False(isCompressed);
        Assert.Same(data, outData);
    }

    [Fact]
    // Larger repeating data compresses then decompresses to the original bytes.
    public void Compress_decompress_round_trip_for_compressible_payload()
    {
        var originalLength = NetCompression.CompressionThreshold + 512;
        var data = new byte[originalLength];
        Array.Fill(data, (byte)9);

        var (compressed, isCompressed) = NetCompression.Compress(data);

        Assert.True(isCompressed);
        Assert.True(compressed.Length < data.Length);

        var restored = NetCompression.Decompress(compressed, originalLength);

        Assert.Equal(data, restored);
    }
}
