using Rex.Shared.Components;

namespace Rex.Shared.Entities.Queries;

/// <summary>
/// Canonical query description built from required and excluded component types.
/// </summary>
public sealed class QueryDescription : IEquatable<QueryDescription>
{
    /// <summary>Empty query with no required or excluded component types.</summary>
    public static QueryDescription Empty { get; } = new();

    /// <summary>Creates an empty query description.</summary>
    public QueryDescription()
        : this(null, null)
    {
    }

    /// <summary>Creates a canonical query description.</summary>
    /// <param name="requiredTypes">Required component types.</param>
    /// <param name="excludedTypes">Excluded component types.</param>
    public QueryDescription(IEnumerable<Type>? requiredTypes = null, IEnumerable<Type>? excludedTypes = null)
    {
        RequiredTypeArray = Canonicalize(requiredTypes);
        ExcludedTypeArray = Canonicalize(excludedTypes);

        for (int i = 0; i < RequiredTypeArray.Length; i++)
        {
            if (Array.BinarySearch(ExcludedTypeArray, RequiredTypeArray[i], TypeKeyComparer.Instance) >= 0)
            {
                throw new InvalidOperationException(
                    $"Component type '{RequiredTypeArray[i].FullName}' cannot be both required and excluded.");
            }
        }
    }

    /// <summary>Gets the canonical required component types.</summary>
    public IReadOnlyList<Type> RequiredTypes => RequiredTypeArray;

    /// <summary>Gets the canonical excluded component types.</summary>
    public IReadOnlyList<Type> ExcludedTypes => ExcludedTypeArray;

    /// <summary>Adds one required component type to the query.</summary>
    /// <typeparam name="T">Component type to require.</typeparam>
    /// <returns>A new query description containing the required type.</returns>
    public QueryDescription With<T>()
        where T : struct, IComponent
    {
        var required = new Type[RequiredTypeArray.Length + 1];
        Array.Copy(RequiredTypeArray, required, RequiredTypeArray.Length);
        required[^1] = typeof(T);
        return new QueryDescription(required, ExcludedTypeArray);
    }

    /// <summary>Adds one excluded component type to the query.</summary>
    /// <typeparam name="T">Component type to exclude.</typeparam>
    /// <returns>A new query description containing the excluded type.</returns>
    public QueryDescription Without<T>()
        where T : struct, IComponent
    {
        var excluded = new Type[ExcludedTypeArray.Length + 1];
        Array.Copy(ExcludedTypeArray, excluded, ExcludedTypeArray.Length);
        excluded[^1] = typeof(T);
        return new QueryDescription(RequiredTypeArray, excluded);
    }

    internal Type[] RequiredTypeArray { get; }

    internal Type[] ExcludedTypeArray { get; }

    /// <summary>Checks whether two query descriptions are canonically equal.</summary>
    /// <param name="other">Other query description to compare.</param>
    /// <returns><see langword="true"/> when both descriptions have the same required and excluded types.</returns>
    public bool Equals(QueryDescription? other)
    {
        if (other is null)
        {
            return false;
        }

        return RequiredTypeArray.AsSpan().SequenceEqual(other.RequiredTypeArray)
               && ExcludedTypeArray.AsSpan().SequenceEqual(other.ExcludedTypeArray);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as QueryDescription);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (Type type in RequiredTypeArray)
        {
            hash.Add(TypeKey(type), StringComparer.Ordinal);
        }

        hash.Add("|", StringComparer.Ordinal);

        foreach (Type type in ExcludedTypeArray)
        {
            hash.Add(TypeKey(type), StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    private static Type[] Canonicalize(IEnumerable<Type>? componentTypes)
    {
        if (componentTypes == null)
        {
            return [];
        }

        Type[] types = componentTypes.ToArray();
        foreach (Type type in types)
        {
            ArgumentNullException.ThrowIfNull(type);
        }

        Array.Sort(types, TypeKeyComparer.Instance);

        if (types.Length == 0)
        {
            return types;
        }

        int uniqueCount = 1;
        for (int i = 1; i < types.Length; i++)
        {
            if (TypeKey(types[i]) == TypeKey(types[i - 1]))
            {
                continue;
            }

            types[uniqueCount++] = types[i];
        }

        if (uniqueCount == types.Length)
        {
            return types;
        }

        var unique = new Type[uniqueCount];
        Array.Copy(types, unique, uniqueCount);
        return unique;
    }

    private static string TypeKey(Type type) => type.AssemblyQualifiedName ?? type.FullName ?? type.Name;

    private sealed class TypeKeyComparer : IComparer<Type>
    {
        public static TypeKeyComparer Instance { get; } = new();

        public int Compare(Type? x, Type? y) => StringComparer.Ordinal.Compare(TypeKey(x!), TypeKey(y!));
    }
}
