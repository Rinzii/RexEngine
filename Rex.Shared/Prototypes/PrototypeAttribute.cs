using Rex.Shared.Serialization.Manager.Attributes;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Marks a type as a loadable prototype definition.
/// </summary>
[MeansDataDefinition]
[AttributeUsage(AttributeTargets.Class)]
public sealed class PrototypeAttribute : Attribute
{
    /// <summary>
    /// Creates a prototype attribute.
    /// </summary>
    /// <param name="type">Optional explicit prototype type name.</param>
    public PrototypeAttribute(string? type = null)
    {
        Type = type;
    }

    /// <summary>Gets the optional explicit prototype type name.</summary>
    public string? Type { get; }
}
