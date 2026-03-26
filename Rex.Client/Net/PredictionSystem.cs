using Rex.Shared.Net.Messages;

namespace Rex.Client.Net;

/// <summary>Client-side movement guess. <see cref="Reconcile"/> snaps to the server then replays unacked inputs.</summary>
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
        // Y not predicted yet. Server owns vertical state for now.
    }

    /// <summary>Reset pose from <paramref name="serverState"/>. Drop inputs through <paramref name="lastProcessedInputTick"/>. Replay the rest.</summary>
    public void Reconcile(EntityState serverState, uint lastProcessedInputTick)
    {
        PredictedX = serverState.X;
        PredictedY = serverState.Y;
        PredictedZ = serverState.Z;

        _inputBuffer.AcknowledgeUpTo(lastProcessedInputTick);

        var unacknowledged = _inputBuffer.GetInputsAfter(lastProcessedInputTick);
        foreach (var input in unacknowledged)
            ApplyInputLocally(input);
    }
}
