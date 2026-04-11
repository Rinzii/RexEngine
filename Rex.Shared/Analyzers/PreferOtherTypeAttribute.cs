#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
///     Warns when the sole generic argument equals <paramref name="genericType"/>. Instantiate with <paramref name="replacementType"/> instead.
/// </summary>
/// <param name="genericType">Type argument that triggers the warning.</param>
/// <param name="replacementType">Concrete type the analyzer suggests.</param>
/// <example>
/// <code>
/// <![CDATA[
///     [PreferOtherTypeAttribute(typeof(int), typeof(MySpecializedType))]
///     public sealed record MyGeneralType<T>(T Field);
///
///     public sealed record MySpecializedType(int Field);
///
///     // Warning RA0021: Use the specific type MySpecializedType instead of MyGeneralType when the type argument is int.
///     var obj = new MyGeneralType<int>(42);
///
///     // No warning.
///     var obj = new MySpecializedType(42);
/// ]]>
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class PreferOtherTypeAttribute(Type genericType, Type replacementType) : Attribute
{
    /// <summary>Generic type parameter that triggers the replacement warning.</summary>
    public readonly Type GenericArgument = genericType;

    /// <summary>Preferred concrete type for the analyzer to suggest.</summary>
    public readonly Type ReplacementType = replacementType;
}
