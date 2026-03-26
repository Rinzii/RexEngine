using Microsoft.CodeAnalysis;

namespace Rex.Analyzers;

public static  class ITypeSymbolExtensions
{
    public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            yield return current;
            current = current.BaseType;
        }
    }
}
