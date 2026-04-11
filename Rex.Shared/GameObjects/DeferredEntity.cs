namespace Rex.Shared.GameObjects;

/// <summary>
/// Placeholder handle for an entity that will be created or spawned later during command-buffer playback.
/// </summary>
public readonly record struct DeferredEntity
{
    private DeferredEntity(int token)
    {
        Token = token;
    }

    /// <summary>Gets the invalid placeholder value.</summary>
    public static DeferredEntity Invalid => default;

    internal int Token { get; }

    /// <summary>Gets a value indicating whether this placeholder can resolve during playback.</summary>
    public bool IsValid => Token > 0;

    internal int Index => Token - 1;

    internal static DeferredEntity FromToken(int token)
    {
        return new DeferredEntity(token);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsValid ? $"DeferredEntity({Token})" : "DeferredEntity(Invalid)";
    }
}
