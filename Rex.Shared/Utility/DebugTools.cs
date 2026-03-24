using JetBrains.Annotations;
using Rex.Shared.Analyzers;

namespace Rex.Shared.Utility;

public static class DebugTools
{
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