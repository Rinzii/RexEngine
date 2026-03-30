using Rex.Shared.Net.Messages;
using Rex.Shared.Simulation;

namespace Rex.Client.Net;

/// <summary>
/// Client-side movement prediction for the local player.
/// </summary>
/// <remarks>
/// <para>
/// Each simulation tick, <see cref="GameClient.Tick"/> stores input in <see cref="InputBuffer"/>,
/// applies the same movement here with <see cref="ApplyInputLocally"/>, and sends the input to the server.
/// The server advances authoritative state and echoes the highest input tick it applied in the next
/// <see cref="WorldSnapshotMessage.LastProcessedInputTick"/>.
/// </para>
/// <para>
/// When a snapshot arrives, <see cref="Reconcile"/> resets the predicted position from the server entity,
/// drops acknowledged inputs from the buffer, then reapplies every still-pending input in tick order.
/// That rewind-and-replay step keeps the client aligned with the server while preserving responsive motion
/// for inputs the server has not consumed yet.
/// </para>
/// </remarks>
public sealed class PredictionSystem
{
    private readonly InputBuffer _inputBuffer;

    /// <summary>Locally predicted world-space X after the last apply or reconcile.</summary>
    public float PredictedX { get; private set; }

    /// <summary>Locally predicted world-space Y after the last apply or reconcile.</summary>
    public float PredictedY { get; private set; }

    /// <summary>Locally predicted world-space Z after the last apply or reconcile.</summary>
    public float PredictedZ { get; private set; }

    public PredictionSystem(InputBuffer inputBuffer)
    {
        _inputBuffer = inputBuffer;
    }

    /// <summary>
    /// Applies one input to the predicted horizontal position using the same direction convention as the shared sim.
    /// </summary>
    /// <remarks>
    /// <see cref="PlayerInputMessage.MoveX"/> and <see cref="PlayerInputMessage.MoveY"/> map to X and Z on the ground plane.
    /// Vertical position is not integrated here so the client does not diverge from server-owned Y until a snapshot arrives.
    /// </remarks>
    public void ApplyInputLocally(PlayerInputMessage input)
    {
        PredictedX = MathF.FusedMultiplyAdd(input.MoveX, MovementConstants.PlanarUnitsPerInputTick, PredictedX);
        PredictedZ = MathF.FusedMultiplyAdd(input.MoveY, MovementConstants.PlanarUnitsPerInputTick, PredictedZ);
        // Y stays at the last reconciled value until Reconcile runs again.
    }

    /// <summary>
    /// Snaps prediction to <paramref name="serverState"/> and replays unacknowledged inputs.
    /// </summary>
    /// <param name="serverState">Authoritative pose for the local player from the snapshot.</param>
    /// <param name="lastProcessedInputTick">
    /// Last input tick the server applied to this player before building the snapshot.
    /// Inputs at or below this tick are treated as acknowledged.
    /// </param>
    /// <remarks>
    /// Call order on the client is snapshot apply for rendering, then this method so the predicted pose matches
    /// the same server tick and input horizon as <see cref="WorldSnapshotMessage"/>.
    /// <see cref="InputBuffer.AcknowledgeUpTo"/> clears slots the server has fully incorporated.
    /// <see cref="InputBuffer.GetInputsAfter"/> returns remaining moves sorted by tick so replay order matches send order.
    /// </remarks>
    public void Reconcile(EntityState serverState, uint lastProcessedInputTick)
    {
        PredictedX = serverState.X;
        PredictedY = serverState.Y;
        PredictedZ = serverState.Z;

        _inputBuffer.AcknowledgeUpTo(lastProcessedInputTick);

        var unacknowledged = _inputBuffer.GetInputsAfter(lastProcessedInputTick);
        foreach (var input in unacknowledged)
        {
            ApplyInputLocally(input);
        }
    }
}
