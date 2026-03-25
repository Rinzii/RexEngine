using Rex.Shared.Net.Messages;

namespace Rex.Client.Net;

/// <summary>
/// Override to sample input from SDL, gamepad, etc.
/// </summary>
public abstract class InputCollector
{
    public abstract PlayerInputMessage Sample(uint tick);
}
