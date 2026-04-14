namespace Rex.Shared.Prototypes;

/// <summary>
/// Event payload describing one prototype manager reload or resource load pass.
/// </summary>
public sealed class PrototypeReloadedEventArgs : EventArgs
{
    /// <summary>
    /// Creates one reload event payload.
    /// </summary>
    /// <param name="version">Monotonic reload version after the load completed.</param>
    /// <param name="sources">Sources that were loaded or reloaded.</param>
    public PrototypeReloadedEventArgs(int version, IReadOnlyList<string> sources)
    {
        Version = version;
        Sources = sources;
    }

    /// <summary>Gets the monotonic reload version.</summary>
    public int Version { get; }

    /// <summary>Gets the sources that were loaded or reloaded.</summary>
    public IReadOnlyList<string> Sources { get; }
}
