namespace Rex.Shared.Analyzers;

/// <summary>
/// <para>
///     Forwards to a method on another type. Descendants should call this proxy instead of the target or the analyzer warns.
/// </para>
/// <para>
///     Parameter list must match the target signature.
/// </para>
/// </summary>
/// <param name="type"><see cref="Type"/> containing the target method.</param>
/// <param name="method">Name of the target method. If null, the name of the proxy method will be used.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ProxyForAttribute(Type type, string? method = null) : Attribute
{
    /// <summary>
    ///     Name of the target method. If null, the name of the proxy method will be used.
    /// </summary>
    public string? Method = method;

    /// <summary>
    ///     <see cref="Type"/> containing the target method.
    /// </summary>
    public Type Type = type;
}
