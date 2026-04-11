namespace Rex.Shared.Prototypes;

/// <summary>
/// Common contract for one loaded prototype instance.
/// </summary>
public interface IPrototype
{
    /// <summary>Gets the stable prototype id.</summary>
    string Id { get; }

    /// <summary>Gets a value indicating whether the prototype is only intended as an authored parent.</summary>
    bool Abstract { get; }
}

/// <summary>
/// Common contract for prototypes supporting inheritance.
/// </summary>
public interface IInheritingPrototype : IPrototype
{
    /// <summary>Gets the ordered parent prototype ids used for inheritance composition.</summary>
    IReadOnlyList<string>? Parents { get; }
}
