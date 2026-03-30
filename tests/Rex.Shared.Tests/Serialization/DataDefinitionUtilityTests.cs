using Rex.Shared.Serialization.Manager.Definition;

namespace Rex.Shared.Tests.Serialization;

// YAML tag strings from member names.
public sealed class DataDefinitionUtilityTests
{
    [Theory]
    [InlineData("Foo", "foo")]
    [InlineData("FooBar", "fooBar")]
    [InlineData("X", "x")]
    // First character becomes lowercase for camelCase tags.
    public void AutoGenerateTag_lowercases_first_character(string input, string expected)
    {
        Assert.Equal(expected, DataDefinitionUtility.AutoGenerateTag(input));
    }
}
