using System;

namespace Rex.Shared.Analyzers;

/// <summary>
///     Implement this interface with explicit interface implementation syntax only.
///     Keeps members off the public surface, for example on initialization helpers.
/// </summary>
/// <example>
/// <code>
///     [RequiresExplicitImplementation]
///     public interface MyInterface
///     {
///         public void DoThing();
///     }
///     <br/>
///     public sealed class MyClass : MyInterface
///     {
///         // Warning RA0000: No explicit interface specified.
///         public void DoThing() { /* ... */ }
///     }
///     <br/>
///     public sealed class MyBetterClass : MyInterface
///     {
///         // No warning.
///         void MyInterface.DoThing() { /* ... */ }
///     }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RequiresExplicitImplementationAttribute : Attribute;
