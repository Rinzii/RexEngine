using Rex.Shared.Net.Messages;

namespace Rex.Client.Input;

/// <summary>
/// Samples player input each tick. Override for SDL, gamepad, etc.
/// </summary>
public abstract class InputCollector
{
    /// <param name="tick">Game tick this sample is for (stored on the message).</param>
    public abstract PlayerInputMessage Sample(uint tick);
}