using JetBrains.Annotations;
using Rex.Shared.Analyzers;

namespace Rex.Shared.Utility;

/// <summary>Checks for debug builds. Throws <see cref="DebugAssertException"/> on failure.</summary>
public static class DebugTools
{
    /// <summary>Throws if <paramref name="arg"/> is null. Message optional.</summary>
    public static void AssertNotNull(
        [AssertionCondition(AssertionConditionType.IS_NOT_NULL)] [System.Diagnostics.CodeAnalysis.NotNull]
        object? arg,
        string? message = null
    )
    {
        if (arg == null)
        {
            throw new DebugAssertException(message ?? "Value cannot be null");
        }
    }
}

/// <summary>Thrown by <see cref="DebugTools"/> assertions. Subclassing allowed. Mark subclasses with <see cref="VirtualAttribute"/>.</summary>
[Virtual]
public class DebugAssertException : Exception
{
    /// <summary>Creates an exception with a default message.</summary>
    public DebugAssertException()
    {
    }

    /// <summary>Creates an exception with <paramref name="message"/>.</summary>
    /// <param name="message">Human readable failure text.</param>
    public DebugAssertException(string? message) : base(message)
    {
    }
}
