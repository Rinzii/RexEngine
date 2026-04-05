using System;

namespace Rex.Shared.Analyzers;

/// <summary>
///     Forbids literal arguments at calls. Use named constants, readonly static fields or validated wrappers instead of raw literals.
/// </summary>
/// <example>
/// <code>
///     public sealed class MyClass
///     {
///         public static bool IsPastry([ForbidLiteral] string id);
///         public static string GrabFromCupboard();
///     }
///     <br/>
///     <br/>
///     // Error RA0023: The id parameter of IsPastry forbids literal values.
///     DebugTools.Assert(MyClass.IsPastry("cupcake"));
///     <br/>
///     var maybePastry = obj.GrabFromCupboard();
///     // Allowed.
///     DebugTools.Assert(MyClass.IsPastry(maybePastry));
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ForbidLiteralAttribute : Attribute;
