using Microsoft.Extensions.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Transfer;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Client.Net;

/// <summary>
/// Client-side networking controller that owns connection flow and inbound message handling.
/// </summary>
public sealed class GameClient
{
    private readonly ILogger _logger;
    private readonly BulkTransferManager _transferManager;
    private IClientNetChannel? _channel;
    private InputCollector? _inputCollector;

    /// <summary>
    /// Gets the client ID assigned by the server.
    /// </summary>
    public int ClientId { get; private set; }

    /// <summary>
    /// Gets the latest replicated world state.
    /// </summary>
    public ClientWorldState WorldState { get; } = new();

    /// <summary>
    /// Gets the buffer of unacknowledged local inputs.
    /// </summary>
    public InputBuffer InputBuffer { get; } = new();

    /// <summary>
    /// Gets the local prediction state for the controlled entity.
    /// </summary>
    public PredictionSystem Prediction { get; }

    /// <summary>
    /// Gets a value that indicates whether the client is connected far enough to exchange gameplay messages.
    /// </summary>
    public bool IsConnected => _channel?.State is ConnectionState.Connected or ConnectionState.Authenticated or ConnectionState.InGame;

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    public ConnectionState State => _channel?.State ?? ConnectionState.Disconnected;

    /// <summary>
    /// Gets the current round trip time in milliseconds.
    /// </summary>
    public int RoundTripTimeMs => _channel?.RoundTripTimeMs ?? 0;

    /// <summary>
    /// Raised when a full bulk payload has been received and reassembled.
    /// </summary>
    public event Action<int, BulkDataType, byte[]>? BulkDataReceived;

    /// <summary>
    /// Creates the client networking controller.
    /// </summary>
    public GameClient(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GameClient>();
        Prediction = new PredictionSystem(InputBuffer);
        _transferManager = new BulkTransferManager(loggerFactory);
        _transferManager.TransferCompleted += OnTransferCompleted;
    }

    /// <summary>
    /// Sets the input source sampled during <see cref="Tick"/>.
    /// </summary>
    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>
    /// Deserializes a completed bulk payload into its protobuf type.
    /// </summary>
    public T DeserializeBulkData<T>(byte[] data)
    {
        return ProtoSerializer.Deserialize<T>(data);
    }

    /// <summary>
    /// Connects to a remote server over LiteNetLib.
    /// </summary>
    public void Connect(string host, int port)
    {
        Connect(new RemoteClientNetChannel(host, port, ProtocolConstants.ConnectionKey));
    }

    /// <summary>
    /// Connects through a transport bridge supplied by Shared.
    /// </summary>
    public void Connect(IClientNetChannel channel)
    {
        NetMessages.RegisterAll();

        SetupChannel(channel);
        channel.Connect();
    }

    /// <summary>
    /// Pumps network events and sends the current input sample when gameplay is active.
    /// </summary>
    public void Tick(uint currentTick)
    {
        _channel?.PollEvents();

        if (_channel?.State != ConnectionState.InGame)
            return;

        if (_inputCollector != null)
        {
            var input = _inputCollector.Sample(currentTick);
            InputBuffer.Store(input);
            Prediction.ApplyInputLocally(input);
            _channel.Send(input);
        }
    }

    /// <summary>
    /// Disconnects from the current server.
    /// </summary>
    public void Disconnect()
    {
        _channel?.Disconnect("Client disconnecting");
    }

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

    private void OnConnected()
    {
        _logger.LogInformation("Connected to server");

        var request = new ConnectRequestMessage(ProtocolConstants.ProtocolVersion, "Player");
        _channel!.Send(request);
    }

    private void OnDisconnected(string reason)
    {
        _logger.LogInformation("Disconnected from server: {Reason}", reason);
    }

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
                _logger.LogDebug("Entity spawned: {EntityId} ({EntityType})", spawn.EntityId, spawn.EntityType);
                break;
            case EntityDestroyMessage destroy:
                _logger.LogDebug("Entity destroyed: {EntityId}", destroy.EntityId);
                break;
        }
    }

    private void HandleConnectResponse(ConnectResponseMessage response)
    {
        if (!response.Accepted)
        {
            _logger.LogWarning("Connection rejected: {Reason}", response.RejectReason);
            _channel?.Disconnect(response.RejectReason ?? "Rejected");
            return;
        }

        ClientId = response.ClientId;
        _channel!.State = ConnectionState.InGame;
        _logger.LogInformation("Connection accepted. ClientId: {ClientId}, TickRate: {TickRate}",
            response.ClientId, response.TickRate);
    }

    /// <summary>
    /// Applies one server snapshot, acks it, then reconciles local prediction.
    /// </summary>
    private void HandleWorldSnapshot(WorldSnapshotMessage snapshot)
    {
        WorldState.ApplySnapshot(snapshot);

        var ack = new StateAckMessage(snapshot.ServerTick);
        _channel?.Send(ack);

        foreach (var entity in snapshot.Entities)
        {
            if (entity.EntityId == ClientId)
            {
                Prediction.Reconcile(entity, snapshot.LastProcessedInputTick);
                break;
            }
        }
    }

    private void OnTransferCompleted(int transferId, BulkDataType dataType, byte[] data)
    {
        _logger.LogInformation("Bulk transfer {TransferId} complete: {DataType} ({Size} bytes)",
            transferId, dataType, data.Length);
        BulkDataReceived?.Invoke(transferId, dataType, data);
    }
}
