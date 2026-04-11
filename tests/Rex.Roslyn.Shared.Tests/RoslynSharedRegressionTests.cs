using System.Collections.Immutable;
using Rex.Roslyn.Shared.Helpers;
using RoslynHash = Rex.Roslyn.Shared.Helpers.HashCode;

namespace Rex.Roslyn.Shared.Tests;

// Locks Roslyn helper types used by source generators.
public sealed class RoslynSharedRegressionTests
{
    [Fact]
    public void Regression_equatable_array_equal_contents_match_hash()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3).AsEquatableArray();
        EquatableArray<int> b = ImmutableArray.Create(1, 2, 3).AsEquatableArray();

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Regression_hash_code_helper_same_add_sequence_matches()
    {
        var a = new RoslynHash();
        a.Add(7);
        a.Add("x");

        var b = new RoslynHash();
        b.Add(7);
        b.Add("x");

        Assert.Equal(a.ToHashCode(), b.ToHashCode());
    }
}
