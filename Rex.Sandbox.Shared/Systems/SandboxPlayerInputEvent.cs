using Rex.Shared.GameObjects;

namespace Rex.Sandbox.Shared.Systems;

/// <summary>
/// Sandbox movement and input event raised with a ref parameter on one controlled entity.
/// </summary>
[ByRefEvent]
public readonly struct SandboxPlayerInputEvent
{
    public SandboxPlayerInputEvent(uint tick, float moveX, float moveY, float lookX, float lookY, uint actionFlags)
    {
        Tick = tick;
        MoveX = moveX;
        MoveY = moveY;
        LookX = lookX;
        LookY = lookY;
        ActionFlags = actionFlags;
    }

    public uint Tick { get; }
    public float MoveX { get; }
    public float MoveY { get; }
    public float LookX { get; }
    public float LookY { get; }
    public uint ActionFlags { get; }
}
