using System.Collections.Immutable;
using Rex.Roslyn.Shared.Helpers;

namespace Rex.Roslyn.Shared.Tests;

// Value equality wrapper over ImmutableArray.
public sealed class EquatableArrayTests
{
    [Fact]
    // Equal items imply Equals and matching hash codes.
    public void Same_contents_compare_equal()
    {
        var a = ImmutableArray.Create("x", "y").AsEquatableArray();
        var b = ImmutableArray.Create("x", "y").AsEquatableArray();

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    // One differing element makes arrays unequal.
    public void Different_contents_compare_unequal()
    {
        var a = ImmutableArray.Create(1, 2, 3).AsEquatableArray();
        var b = ImmutableArray.Create(1, 2, 4).AsEquatableArray();

        Assert.NotEqual(a, b);
    }

    [Fact]
    // Two empty arrays compare equal and report IsEmpty.
    public void Empty_arrays_are_equal()
    {
        var a = ImmutableArray<string>.Empty.AsEquatableArray();
        var b = ImmutableArray<string>.Empty.AsEquatableArray();
        Assert.Equal(a, b);
        Assert.True(a.IsEmpty);
    }
}
