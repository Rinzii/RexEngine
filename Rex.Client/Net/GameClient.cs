using Microsoft.Extensions.Logging;
using Rex.Client.Input;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Client.Net;

/// <summary>
/// Client-side networking controller. Owns connection flow and inbound message handling.
/// </summary>
/// <remarks>
/// <para>
/// The host application constructs one instance for <see cref="NetMode.Client"/> (or listen-server mode after the child server is up).
/// Call <see cref="Connect(string, int)"/> or <see cref="Connect(IClientNetChannel)"/> once, then call <see cref="Tick"/> on every simulation tick.
/// The transport must call <see cref="IClientNetChannel.PollEvents"/> through <see cref="Tick"/> so receives and connection callbacks run.
/// </para>
/// <para>
/// After <see cref="ConnectResponseMessage"/> accepts, this type stores <see cref="ClientId"/> and <see cref="LocalPlayerEntityId"/>,
/// marks the channel <see cref="ConnectionState.InGame"/>, and begins sampling input, prediction, and snapshot-driven reconcile.
/// </para>
/// </remarks>
public sealed partial class GameClient
{
    private readonly ILogger _logger;
    private readonly BulkTransferManager _transferManager;

    // Null until Connect assigns a channel. Replaced when Connect(IClientNetChannel) runs again.
    private IClientNetChannel? _channel;

    // Optional. When null, Tick still polls the network but does not send PlayerInputMessage.
    private InputCollector? _inputCollector;

    /// <summary>Assigned by the server after accept. Empty until then.</summary>
    public Guid ClientId { get; private set; }

    /// <summary>Server entity id for the local player after accept. Zero until then.</summary>
    public int LocalPlayerEntityId { get; private set; }

    /// <summary>Last two snapshots for render interpolation.</summary>
    public ClientWorldState WorldState { get; } = new();

    /// <summary>Inputs kept for prediction replay after snapshot reconcile.</summary>
    public InputBuffer InputBuffer { get; } = new();

    /// <summary>Local movement prediction. See <see cref="PredictionSystem"/>.</summary>
    public PredictionSystem Prediction { get; }

    /// <summary>True while the transport is connected, authenticated, or in-game.</summary>
    public bool IsConnected => _channel?.State is ConnectionState.Connected
        or ConnectionState.Authenticated or ConnectionState.InGame;

    /// <summary>Current <see cref="IClientNetChannel.State"/>, or disconnected when no channel exists.</summary>
    public ConnectionState State => _channel?.State ?? ConnectionState.Disconnected;

    /// <summary>Round-trip time from the transport in milliseconds, or zero when unknown.</summary>
    public int RoundTripTimeMs => _channel?.RoundTripTimeMs ?? 0;

    /// <summary>Fired when a bulk transfer finishes reassembly on this client.</summary>
    public event Action<Guid, BulkDataType, byte[]>? BulkDataReceived;

    /// <summary>Creates a client with logging, bulk transfer handling, and a prediction stack tied to <see cref="InputBuffer"/>.</summary>
    /// <param name="loggerFactory">Factory used for this type and nested systems such as <see cref="BulkTransferManager"/>.</param>
    public GameClient(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GameClient>();
        Prediction = new PredictionSystem(InputBuffer);
        _transferManager = new BulkTransferManager(loggerFactory);
        _transferManager.TransferCompleted += OnTransferCompleted;
    }

    /// <summary>Registers the sampler used each tick to build <see cref="PlayerInputMessage"/>.</summary>
    /// <param name="collector">Source of per-tick input. May be set before or after <see cref="Connect"/>.</param>
    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>Runs protobuf-net deserialize on a completed bulk payload.</summary>
    /// <typeparam name="T">Contract type expected in the payload.</typeparam>
    /// <param name="data">Raw bytes from <see cref="BulkDataReceived"/>.</param>
    public static T DeserializeBulkData<T>(byte[] data)
    {
        return ProtoSerializer.Deserialize<T>(data);
    }

    /// <summary>Connects to a remote server over LiteNetLib using <see cref="ProtocolConstants.ConnectionKey"/>.</summary>
    /// <param name="host">Server host name or address.</param>
    /// <param name="port">Server UDP port.</param>
    public void Connect(string host, int port)
    {
        Connect(new RemoteClientNetChannel(host, port, ProtocolConstants.ConnectionKey, _logger));
    }

    /// <summary>Wires message registration, subscribes to the channel, and starts the transport handshake.</summary>
    /// <param name="channel">Concrete transport such as <see cref="RemoteClientNetChannel"/> or a test double.</param>
    /// <remarks>
    /// <see cref="NetMessages.RegisterAll"/> is idempotent and safe to call on every connect attempt.
    /// <see cref="IClientNetChannel.Connect"/> is asynchronous. <see cref="OnConnected"/> sends <see cref="ConnectRequestMessage"/> when the transport reports success.
    /// </remarks>
    public void Connect(IClientNetChannel channel)
    {
        NetMessages.RegisterAll();
        SetupChannel(channel);
        channel.Connect();
    }

    /// <summary>Polls the transport and, when in-game, samples input, prediction, and outbound sends.</summary>
    /// <param name="currentTick">Simulation tick index from <see cref="Rex.Shared.Timing.TickClock"/>.</param>
    /// <remarks>
    /// <see cref="IClientNetChannel.PollEvents"/> runs first so pending <see cref="WorldSnapshotMessage"/> and connection events are handled before this tick sends input.
    /// Input uses the default <see cref="MessageGroup"/> delivery (unreliable) unless the caller overrides <see cref="IClientNetChannel.Send(INetMessage, byte, DeliveryMethod)"/>.
    /// </remarks>
    public void Tick(uint currentTick)
    {
        _channel?.PollEvents();

        if (_channel?.State != ConnectionState.InGame)
        {
            return;
        }

        if (_inputCollector != null)
        {
            var input = _inputCollector.Sample(currentTick);
            InputBuffer.Store(input);
            Prediction.ApplyInputLocally(input);
            // PlayerInputMessage maps to MessageGroup.Input (unreliable) unless the channel overrides Send.
            _channel.Send(input);
        }
    }

    /// <summary>Disconnects from the server using a generic client-side reason string.</summary>
    public void Disconnect()
    {
        _channel?.Disconnect("Client disconnecting");
    }

    /// <summary>Unsubscribes any previous channel, attaches handlers, and stores the new instance.</summary>
    private void SetupChannel(IClientNetChannel channel)
    {
        if (_channel != null)
        {
            _channel.MessageReceived -= OnMessageReceived;
            _channel.Connected -= OnConnected;
            _channel.Disconnected -= OnDisconnected;
        }

        _channel = channel;
        channel.MessageReceived += OnMessageReceived;
        channel.Connected += OnConnected;
        channel.Disconnected += OnDisconnected;
    }

    /// <summary>Transport callback after the server accepts the LiteNetLib connection.</summary>
    private void OnConnected()
    {
        LogConnectedToServer();
        _channel!.Send(new ConnectRequestMessage(ProtocolConstants.ProtocolVersion, "Player"));
    }

    /// <summary>Transport callback when the connection drops.</summary>
    private void OnDisconnected(string reason)
    {
        LogDisconnected(reason);
    }

    /// <summary>Despatch table for inbound <see cref="INetMessage"/> types this client understands.</summary>
    private void OnMessageReceived(INetMessage message)
    {
        switch (message)
        {
            case ConnectResponseMessage response:
                HandleConnectResponse(response);
                break;
            case WorldSnapshotMessage snapshot:
                HandleWorldSnapshot(snapshot);
                break;
            case BulkTransferInitMessage transferInit:
                _transferManager.HandleTransferInit(transferInit);
                break;
            case BulkTransferChunkMessage transferChunk:
                _transferManager.HandleTransferChunk(transferChunk);
                break;
            case EntitySpawnMessage spawn:
                LogEntitySpawned(spawn.EntityId, spawn.EntityType);
                break;
            case EntityDestroyMessage destroy:
                LogEntityDestroyed(destroy.EntityId);
                break;
            default:
                LogUnhandledNetMessage(message.MessageId, message.GetType().Name);
                break;
        }
    }

    /// <summary>Applies handshake results, updates identity fields, or disconnects on rejection.</summary>
    private void HandleConnectResponse(ConnectResponseMessage response)
    {
        if (!response.Accepted)
        {
            LogConnectionRejected(response.RejectReason);
            _channel?.Disconnect(response.RejectReason ?? "Rejected");
            return;
        }

        ClientId = response.ClientId;
        LocalPlayerEntityId = response.LocalPlayerEntityId;
        _channel!.State = ConnectionState.InGame;
        LogConnectionAccepted(response.ClientId, response.TickRate);
    }

    /// <summary>Updates render interpolation state, acks the snapshot tick, and reconciles prediction for the local player.</summary>
    private void HandleWorldSnapshot(WorldSnapshotMessage snapshot)
    {
        WorldState.ApplySnapshot(snapshot);
        _channel?.Send(new StateAckMessage(snapshot.ServerTick));

        foreach (var entity in snapshot.Entities)
        {
            if (entity.EntityId == LocalPlayerEntityId)
            {
                Prediction.Reconcile(entity, snapshot.LastProcessedInputTick);
                break;
            }
        }
    }

    /// <summary>Raises <see cref="BulkDataReceived"/> after logging completion.</summary>
    private void OnTransferCompleted(Guid transferId, BulkDataType dataType, byte[] data)
    {
        LogClientBulkTransferComplete(transferId, dataType, data.Length);

        BulkDataReceived?.Invoke(transferId, dataType, data);
    }
}
