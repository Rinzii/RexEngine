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
public sealed class GameClient
{
    private readonly ILogger _logger;
    private readonly BulkTransferManager _transferManager;
    private IClientNetChannel? _channel;
    private InputCollector? _inputCollector;

    /// <summary>Assigned by the server after accept. Zero until then.</summary>
    public int ClientId { get; private set; }

    /// <summary>Last two snapshots for render interpolation.</summary>
    public ClientWorldState WorldState { get; } = new();

    /// <summary>Inputs kept for prediction replay after snapshot reconcile.</summary>
    public InputBuffer InputBuffer { get; } = new();

    /// <summary>Local movement prediction. See <see cref="PredictionSystem"/>.</summary>
    public PredictionSystem Prediction { get; }

    /// <summary>True while the transport is connected, authenticated, or in-game.</summary>
    public bool IsConnected => _channel?.State is ConnectionState.Connected
        or ConnectionState.Authenticated or ConnectionState.InGame;

    public ConnectionState State => _channel?.State ?? ConnectionState.Disconnected;

    public int RoundTripTimeMs => _channel?.RoundTripTimeMs ?? 0;

    /// <summary>Fired when a bulk transfer finishes reassembly on this client.</summary>
    public event Action<int, BulkDataType, byte[]>? BulkDataReceived;

    public GameClient(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GameClient>();
        Prediction = new PredictionSystem(InputBuffer);
        _transferManager = new BulkTransferManager(loggerFactory);
        _transferManager.TransferCompleted += OnTransferCompleted;
    }

    public void SetInputCollector(InputCollector collector)
    {
        _inputCollector = collector;
    }

    /// <summary>Runs protobuf-net deserialize on a completed bulk payload.</summary>
    public T DeserializeBulkData<T>(byte[] data)
    {
        return ProtoSerializer.Deserialize<T>(data);
    }

    /// <summary>Connects to a remote server over LiteNetLib.</summary>
    public void Connect(string host, int port)
    {
        Connect(new RemoteClientNetChannel(host, port, ProtocolConstants.ConnectionKey));
    }

    /// <summary>Connects through an arbitrary transport channel.</summary>
    public void Connect(IClientNetChannel channel)
    {
        NetMessages.RegisterAll();
        SetupChannel(channel);
        channel.Connect();
    }

    /// <summary>Pumps network events and sends input when in-game.</summary>
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
            _channel.Send(input); // unreliable lane by default for input group
        }
    }

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
        _channel!.Send(new ConnectRequestMessage(ProtocolConstants.ProtocolVersion, "Player"));
    }

    private void OnDisconnected(string reason)
    {
        _logger.LogInformation("Disconnected: {Reason}", reason);
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
        _logger.LogInformation("Accepted. ClientId: {ClientId}, TickRate: {TickRate}",
            response.ClientId, response.TickRate);
    }

    private void HandleWorldSnapshot(WorldSnapshotMessage snapshot)
    {
        WorldState.ApplySnapshot(snapshot);
        _channel?.Send(new StateAckMessage(snapshot.ServerTick));

        // Client id doubles as controlled entity id on this prototype server.
        foreach (var entity in snapshot.Entities)
            if (entity.EntityId == ClientId)
            {
                Prediction.Reconcile(entity, snapshot.LastProcessedInputTick);
                break;
            }
    }

    private void OnTransferCompleted(int transferId, BulkDataType dataType, byte[] data)
    {
        _logger.LogInformation("Bulk transfer {TransferId} complete: {DataType} ({Size} bytes)",
            transferId, dataType, data.Length);
        BulkDataReceived?.Invoke(transferId, dataType, data);
    }
}