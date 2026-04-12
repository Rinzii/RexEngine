using System.Collections.Immutable;
using System.Text;
using Rex.Roslyn.Shared.Helpers;

namespace Rex.Roslyn.Shared.Tests;

public sealed class PartialTypeInfoTests
{
    [Theory]
    [InlineData(Microsoft.CodeAnalysis.Accessibility.Public, "public partial class Widget")]
    [InlineData(Microsoft.CodeAnalysis.Accessibility.NotApplicable, "public partial class Widget")]
    public void WriteHeader_maps_accessibility_to_valid_keyword(Microsoft.CodeAnalysis.Accessibility accessibility, string expected)
    {
        PartialTypeInfo info = new(
            Namespace: null,
            Name: "Widget",
            DisplayName: "Widget",
            TypeParameterNames: ImmutableArray<string>.Empty.AsEquatableArray(),
            IsValid: true,
            SyntaxLocation: Microsoft.CodeAnalysis.Location.None,
            Accessibility: accessibility,
            Kind: Microsoft.CodeAnalysis.TypeKind.Class,
            IsRecord: false,
            IsAbstract: false);

        StringBuilder builder = new();
        info.WriteHeader(builder);

        Assert.Equal(expected, builder.ToString());
    }
}
