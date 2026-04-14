namespace Rex.Shared.Analyzers;

/// <summary>
///     Game content must not implement this interface directly.
/// </summary>
/// <remarks>
/// <para>
///     The engine can add members on minor updates, so a manual implementation drifts from the real contract.
/// </para>
/// <para>
///     No analyzer enforces it today. Following the rule still avoids breaks when engine interfaces expand.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class NotContentImplementableAttribute : Attribute;
