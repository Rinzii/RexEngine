#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
///     Warns when callers pass <see langword="typeof"/> while a generic overload exists. Call the generic overload instead to avoid extra allocations on those paths.
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
///     public sealed MyClass
///     {
///         [PreferGenericVariant]
///         public static bool IsPastry(Type t);
///         public static bool IsPastry<T>();
///     }
///
///     // Warning RA0005: Consider using the generic variant of this method to avoid potential allocations.
///     MyClass.IsPastry(typeof(Cupcake));
/// ]]>
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PreferGenericVariantAttribute : Attribute
{
    /// <summary>Names the preferred generic overload for the analyzer.</summary>
    public readonly string GenericVariant;

    /// <summary>Stores the optional overload hint for the analyzer.</summary>
    /// <param name="genericVariant">Token owned by the analyzer that describes the replacement API.</param>
    public PreferGenericVariantAttribute(string genericVariant = null!)
    {
        GenericVariant = genericVariant;
    }
}
