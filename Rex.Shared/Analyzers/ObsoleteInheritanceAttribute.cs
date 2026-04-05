using System;

namespace Rex.Shared.Analyzers;

/// <summary>
///     Subclassing this type is obsolete and should surface a warning.
/// </summary>
/// <remarks>
///     For types that should not have carried <see cref="VirtualAttribute"/>.
/// </remarks>
/// <example>
/// <code>
///     [ObsoleteInheritance]
///     public class MyClass;
///     <br/>
///     // Warning RA0024: Type 'MyDescendant' inherits from 'MyClass', which has obsoleted inheriting from itself.
///     public sealed class MyDescendant : MyClass;
/// </code>
/// </example>
/// <seealso cref="VirtualAttribute"/>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ObsoleteInheritanceAttribute : Attribute
{
    /// <summary>
    /// An optional message provided alongside this obsoletion.
    /// </summary>
    public string? Message { get; }

    /// <summary>Uses the default analyzer message.</summary>
    public ObsoleteInheritanceAttribute()
    {
    }

    /// <summary>Sets custom diagnostic text.</summary>
    /// <param name="message">Text shown in the diagnostic.</param>
    public ObsoleteInheritanceAttribute(string message)
    {
        Message = message;
    }
}
