using Rex.Shared.Net.Messages;

namespace Rex.Client.Input;

/// <summary>Samples player input each fixed tick. Subclass for SDL, gamepad, or other backends.</summary>
public abstract class InputCollector
{
    /// <param name="tick">Tick index stored on the returned <see cref="PlayerInputMessage"/>.</param>
    public abstract PlayerInputMessage Sample(uint tick);
}