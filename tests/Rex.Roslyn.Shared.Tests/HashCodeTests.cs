using RoslynHash = Rex.Roslyn.Shared.Helpers.HashCode;

namespace Rex.Roslyn.Shared.Tests;

// HashCode helper used by source generators.
public sealed class HashCodeTests
{
    [Fact]
    // Same Add sequence yields the same ToHashCode.
    public void Same_sequence_produces_same_hash()
    {
        var a = new RoslynHash();
        a.Add(1);
        a.Add(2);

        var b = new RoslynHash();
        b.Add(1);
        b.Add(2);

        Assert.Equal(a.ToHashCode(), b.ToHashCode());
    }
}
