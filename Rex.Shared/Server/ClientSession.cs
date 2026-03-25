using Rex.Shared.Net;
using Rex.Shared.Net.Messages;

namespace Rex.Shared.Server;

/// <summary>
/// Per-client networking state tracked by the server.
/// </summary>
public sealed class ClientSession
{
    /// <summary>
    /// Gets the transport used for this client.
    /// </summary>
    public IServerNetChannel Channel { get; }

    /// <summary>
    /// Gets the client ID assigned by the server.
    /// </summary>
    public int ClientId => Channel.ClientId;

    /// <summary>
    /// Gets or sets the player name reported during handshake.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest input tick the server applied for this client.
    /// </summary>
    public uint LastProcessedInputTick { get; set; }

    /// <summary>
    /// Gets or sets the latest snapshot tick acknowledged by this client.
    /// </summary>
    public uint LastAcknowledgedTick { get; set; }

    private readonly Queue<PlayerInputMessage> _inputBuffer = new();

    /// <summary>
    /// Creates a server session around a transport channel.
    /// </summary>
    public ClientSession(IServerNetChannel channel)
    {
        Channel = channel;
    }

    /// <summary>
    /// Queues one input message for simulation.
    /// </summary>
    public void EnqueueInput(PlayerInputMessage input)
    {
        _inputBuffer.Enqueue(input);
    }

    /// <summary>
    /// Tries to dequeue one buffered input message.
    /// </summary>
    public bool TryDequeueInput(out PlayerInputMessage? input)
    {
        return _inputBuffer.TryDequeue(out input);
    }
}
