using System;

#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
///     Prefer non-generic overloads when the type arguments match <see cref="ForTypes"/> instead of the generic overload.
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
///     public sealed MyClass
///     {
///         [PreferNonGenericVariantFor(typeof(Cupcake))]
///         public static string DescribeFood<T>(T food);
///         public static string DescribeCupcake(Cupcake food);
///     }
///
///     // Warning RA0020: Use the non-generic overload for Cupcake instead of the generic method.
///     MyClass.DescribeFood<Cupcake>(new Cupcake());
///
///     // No warning
///     MyClass.DescribeCupcake(new Cupcake());
/// ]]>
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PreferNonGenericVariantForAttribute : Attribute
{
    /// <summary>
    ///     Each entry names a type argument that should use a non-generic overload.
    /// </summary>
    public readonly Type[] ForTypes;

    /// <summary>Type arguments that steer the analyzer toward non-generic overloads.</summary>
    public PreferNonGenericVariantForAttribute(params Type[] forTypes)
    {
        ForTypes = forTypes;
    }
}
