using System;

#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
///     Marks that use of a generic Type should be replaced with a specific other Type
///     when the type argument T is a certain Type.
/// </summary>
/// <param name="genericType">The type that, when used as the sole generic argument, should trigger the warning.</param>
/// <param name="replacementType">The type that you should replace the usage with.</param>
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
    public readonly Type GenericArgument = genericType;
    public readonly Type ReplacementType = replacementType;
}
