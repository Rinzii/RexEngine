namespace Rex.Shared.Net.Replication;

/// <summary>
/// Serialized ECS component payload for one replicated entity inside one network snapshot.
/// </summary>
public sealed class ReplicatedComponentState
{
    /// <summary>
    /// Creates one serialized replicated component payload.
    /// </summary>
    public ReplicatedComponentState(int componentId, byte[] payload)
    {
        if (componentId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentId), componentId, "Component id must be positive.");
        }

        ArgumentNullException.ThrowIfNull(payload);
        ComponentId = componentId;
        Payload = payload;
    }

    /// <summary>Gets the stable replicated component id.</summary>
    public int ComponentId { get; }

    /// <summary>Gets the serialized component payload bytes.</summary>
    public byte[] Payload { get; }
}
