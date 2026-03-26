using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Server.Simulation;

/// <summary>Per-client state tracked by the server host.</summary>
public sealed class ClientSession
{
    public IServerNetChannel Channel { get; }
    public int ClientId => Channel.ClientId;
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>Highest input tick applied to sim this session (sent back in snapshots).</summary>
    public uint LastProcessedInputTick { get; set; }

    /// <summary>Last snapshot tick the client acked. Drives delta vs full broadcast.</summary>
    public uint LastAcknowledgedTick { get; set; }

    private readonly Queue<PlayerInputMessage> _inputBuffer = new();

    public ClientSession(IServerNetChannel channel)
    {
        Channel = channel;
    }

    public void EnqueueInput(PlayerInputMessage input)
    {
        _inputBuffer.Enqueue(input);
    }

    public bool TryDequeueInput(out PlayerInputMessage? input)
    {
        return _inputBuffer.TryDequeue(out input);
    }
}