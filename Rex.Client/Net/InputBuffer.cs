using Rex.Shared.Net.Messages;
using Rex.Shared.Utility;

namespace Rex.Client.Net;

/// <summary>
/// Ring buffer of unacknowledged inputs for prediction and reconciliation.
/// </summary>
public sealed class InputBuffer
{
    private readonly TickRingBuffer<PlayerInputMessage?> _buffer;

    public InputBuffer(int capacity = 128)
    {
        _buffer = new TickRingBuffer<PlayerInputMessage?>(capacity);
    }

    public void Store(PlayerInputMessage input)
    {
        var slot = _buffer.GetSlot(input.Tick);
        slot.Tick = input.Tick;
        slot.IsAssigned = true;
        slot.Value = input;
    }

    /// <summary>Clears inputs the server has definitely applied (tick &lt;= ack).</summary>
    public void AcknowledgeUpTo(uint tick)
    {
        for (var i = 0; i < _buffer.Capacity; i++)
        {
            var slot = _buffer.GetSlotAt(i);
            if (slot.IsAssigned && slot.Value != null && slot.Tick <= tick)
            {
                slot.IsAssigned = false;
                slot.Value = null;
            }
        }
    }

    /// <summary>Inputs with tick greater than <paramref name="tick"/>, ordered by tick ascending.</summary>
    public IReadOnlyList<PlayerInputMessage> GetInputsAfter(uint tick)
    {
        var result = new List<PlayerInputMessage>();

        for (var i = 0; i < _buffer.Capacity; i++)
        {
            var slot = _buffer.GetSlotAt(i);
            if (slot.IsAssigned && slot.Value != null && slot.Tick > tick)
                result.Add(slot.Value);
        }

        result.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        return result;
    }
}