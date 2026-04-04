using Rex.Sandbox.Shared.Net.Messages;

namespace Rex.Sandbox.Client.Input;

/// <summary>
/// Samples Sandbox player input each fixed tick.
/// </summary>
public abstract class InputCollector
{
    public abstract PlayerInputMessage Sample(uint tick);
}
