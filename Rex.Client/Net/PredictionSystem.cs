using Rex.Shared.Net.Messages;

namespace Rex.Client.Net;

/// <summary>
/// Applies inputs locally for immediate feedback, reconciles when the server state arrives.
/// </summary>
public sealed class PredictionSystem
{
    private readonly InputBuffer _inputBuffer;

    public float PredictedX { get; private set; }
    public float PredictedY { get; private set; }
    public float PredictedZ { get; private set; }

    public PredictionSystem(InputBuffer inputBuffer)
    {
        _inputBuffer = inputBuffer;
    }

    public void ApplyInputLocally(PlayerInputMessage input)
    {
        const float moveSpeed = 5.0f;
        PredictedX += input.MoveX * moveSpeed;
        PredictedZ += input.MoveY * moveSpeed;
    }

    /// <summary>
    /// Snaps to server state and replays unacknowledged inputs on top.
    /// </summary>
    public void Reconcile(EntityState serverState, uint lastProcessedInputTick)
    {
        // Snap to server's authoritative position.
        PredictedX = serverState.X;
        PredictedY = serverState.Y;
        PredictedZ = serverState.Z;

        // Acknowledge processed inputs.
        _inputBuffer.AcknowledgeUpTo(lastProcessedInputTick);

        // Re-apply any unacknowledged inputs on top of the server state.
        var unacknowledged = _inputBuffer.GetInputsAfter(lastProcessedInputTick);
        foreach (var input in unacknowledged)
        {
            ApplyInputLocally(input);
        }
    }
}
