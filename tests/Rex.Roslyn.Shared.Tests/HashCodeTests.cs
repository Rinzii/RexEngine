using RoslynHash = Rex.Roslyn.Shared.Helpers.HashCode;

namespace Rex.Roslyn.Shared.Tests;

public sealed class HashCodeTests
{
    [Fact]
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
