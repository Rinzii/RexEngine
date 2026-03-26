using JetBrains.Annotations;
using Rex.Shared.Analyzers;

namespace Rex.Shared.Utility;

/// <summary>Debug-only checks. Throws <see cref="DebugAssertException"/> on failure.</summary>
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

/// <summary>Thrown by <see cref="DebugTools"/> assertions. Subclassing allowed (see <c>Virtual</c> analyzer).</summary>
[Virtual]
public class DebugAssertException : Exception
{
    public DebugAssertException()
    {
    }
    
    public DebugAssertException(string? message) : base(message)
    {
    }
}