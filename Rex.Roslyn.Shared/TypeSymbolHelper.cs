// ReSharper disable once RedundantNullableDirective

#pragma warning disable IDE0240
#nullable enable
#pragma warning restore IDE0240

using Microsoft.CodeAnalysis;

namespace Rex.Roslyn.Shared;

public static class TypeSymbolHelper
{
    public static bool ShittyTypeMatch(ITypeSymbol type, string attributeMetadataName)
    {
        // Doing it like this only allocates when the type actually matches, which is good enough for me right now.
        if (!attributeMetadataName.EndsWith(type.Name))
        {
            return false;
        }

        return type.ToDisplayString() == attributeMetadataName;
    }

    public static bool ImplementsInterface(ITypeSymbol type, string interfaceTypeName)
    {
        foreach (INamedTypeSymbol? interfaceType in type.AllInterfaces)
        {
            if (ShittyTypeMatch(interfaceType, interfaceTypeName))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ImplementsInterface(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        foreach (INamedTypeSymbol? @interface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(@interface, interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// All members on <paramref name="type"/> and its base types, walking upward.
    /// Covers components whose abstract bases declare autonetworked data fields.
    /// </summary>
    public static IEnumerable<ISymbol> GetAllMembersIncludingInherited(INamedTypeSymbol type)
    {
        INamedTypeSymbol? current = type;
        while (current != null)
        {
            foreach (ISymbol? member in current.GetMembers())
            {
                yield return member;
            }

            current = current.BaseType;
        }
    }

    /// <summary>
    /// If <paramref name="type"/> is a Nullable{T}, returns the <see cref="ITypeSymbol"/> of the underlying type.
    /// Otherwise, returns <paramref name="type"/>.
    /// </summary>
    // Modified from https://www.meziantou.net/working-with-types-in-a-roslyn-analyzer.htm
    public static ITypeSymbol GetNullableUnderlyingTypeOrSelf(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T } namedType)
        {
            return namedType.TypeArguments[0];
        }

        return type;
    }

    /// <summary>
    /// Enumerates all base types of the given <paramref name="type"/>.
    /// </summary>
    public static IEnumerable<ITypeSymbol> GetBaseTypes(ITypeSymbol type)
    {
        INamedTypeSymbol? baseType = type.BaseType;
        while (baseType != null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }

    /// <summary>
    /// Checks if the given <paramref name="type"/> inherits from <paramref name="other"/>.
    /// </summary>
    /// <returns>True if <paramref name="type"/> inherits from <paramref name="other"/>, otherwise false.</returns>
    public static bool Inherits(ITypeSymbol type, ITypeSymbol other)
    {
        foreach (ITypeSymbol? baseType in GetBaseTypes(type))
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, other))
            {
                return true;
            }
        }

        return false;
    }
}
