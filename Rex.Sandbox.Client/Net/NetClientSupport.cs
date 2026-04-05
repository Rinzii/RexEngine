using Microsoft.Extensions.Logging;
using Rex.Client.Net;
using Rex.Sandbox.Client.Input;
using Rex.Sandbox.Shared.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Net.Transfer;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Transfer;
using Rex.Shared.Utility;
using ConnectionState = Rex.Shared.Net.ConnectionState;

namespace Rex.Sandbox.Client.Net;

/// <summary>
/// Networking controller for the Sandbox sample consumer that ships with this repository.
/// </summary>
public sealed partial class GameClient
{
    private readonly ILogger _logger;
    private readonly BulkTransferManager _transferManager;
    private IClientNetChannel? _channel;
    private InputCollector? _inputCollector;

    public Guid ClientId { get; private set; }
    public int LocalPlayerEntityId { get; private set; }
    public ClientWorldState WorldState { get; } = new();
    public InputBuffer InputBuffer { get; } = new();
    public PredictionSystem Prediction { get; }
    public bool IsConnected => _channel?.State is ConnectionState.Connected
        or ConnectionState.Authenticated or ConnectionState.InGame;
    public ConnectionState State => _channel?.State ?? ConnectionState.Disconnected;
    public int RoundTripTimeMs => _channel?.RoundTripTimeMs ?? 0;

    public event Action<Guid, SandboxBulkDataType, byte[]>? BulkDataReceived;

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

    public static T DeserializeBulkData<T>(byte[] data)
    {
        return ProtoSerializer.Deserialize<T>(data);
    }

    public void Connect(string host, int port)
    {
        Connect(new RemoteClientNetChannel(host, port, SandboxProtocolConstants.ConnectionKey, _logger));
    }

    public void Connect(IClientNetChannel channel)
    {
        SandboxNetMessages.RegisterAll();
        SetupChannel(channel);
        channel.Connect();
    }

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
            _channel.Send(input);
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
        LogConnectedToServer();
        _channel!.Send(new ConnectRequestMessage(ProtocolConstants.ProtocolVersion, "Player"));
    }

    private void OnDisconnected(string reason)
    {
        LogDisconnected(reason);
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

    private void HandleWorldSnapshot(WorldSnapshotMessage snapshot)
    {
        WorldState.ApplySnapshot(snapshot);
        _channel?.Send(new Rex.Shared.Net.Messages.StateAckMessage(snapshot.ServerTick));

        foreach (var entity in snapshot.Entities)
        {
            if (entity.EntityId == LocalPlayerEntityId)
            {
                Prediction.Reconcile(entity, snapshot.LastProcessedInputTick);
                break;
            }
        }
    }

    private void OnTransferCompleted(Guid transferId, byte dataType, byte[] data)
    {
        var sandboxDataType = (SandboxBulkDataType)dataType;
        LogClientBulkTransferComplete(transferId, sandboxDataType, data.Length);
        BulkDataReceived?.Invoke(transferId, sandboxDataType, data);
    }
}

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

    public void AcknowledgeUpTo(uint tick)
    {
        for (var i = 0; i < _buffer.Capacity; i++)
        {
            var slot = _buffer.GetSlotAt(i);
            if (slot is { IsAssigned: true, Value: not null } && slot.Tick <= tick)
            {
                slot.IsAssigned = false;
                slot.Value = null;
            }
        }
    }

    public IReadOnlyList<PlayerInputMessage> GetInputsAfter(uint tick)
    {
        var result = new List<PlayerInputMessage>();

        for (var i = 0; i < _buffer.Capacity; i++)
        {
            var slot = _buffer.GetSlotAt(i);
            if (slot is { IsAssigned: true, Value: not null } && slot.Tick > tick)
            {
                result.Add(slot.Value);
            }
        }

        result.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        return result;
    }
}

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
        PredictedX = MathF.FusedMultiplyAdd(input.MoveX, MovementConstants.PlanarUnitsPerInputTick, PredictedX);
        PredictedZ = MathF.FusedMultiplyAdd(input.MoveY, MovementConstants.PlanarUnitsPerInputTick, PredictedZ);
    }

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

public sealed class ClientWorldState
{
    private WorldSnapshotMessage? _previousSnapshot;
    private WorldSnapshotMessage? _currentSnapshot;

    public WorldSnapshotMessage? CurrentSnapshot => _currentSnapshot;
    public uint LastServerTick => _currentSnapshot?.ServerTick ?? 0;

    public void ApplySnapshot(WorldSnapshotMessage snapshot)
    {
        _previousSnapshot = _currentSnapshot;
        _currentSnapshot = snapshot;
    }
    public IReadOnlyList<EntityState> GetInterpolatedState(float alpha)
    {
        if (_currentSnapshot == null)
        {
            return [];
        }

        if (_previousSnapshot == null)
        {
            return _currentSnapshot.Entities;
        }

        var result = new List<EntityState>();
        var previousEntities = new Dictionary<int, EntityState>();

        foreach (var entity in _previousSnapshot.Entities)
        {
            previousEntities[entity.EntityId] = entity;
        }

        foreach (var current in _currentSnapshot.Entities)
        {
            if (previousEntities.TryGetValue(current.EntityId, out var previous))
            {
                result.Add(EntityStateInterpolation.Lerp(previous, current, alpha));
            }
            else
            {
                result.Add(current);
            }
        }

        return result;
    }
}

public sealed partial class GameClient
{
    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectedToServer, Level = LogLevel.Information,
        Message = "Connected to Sandbox server")]
    private partial void LogConnectedToServer();

    [LoggerMessage(EventId = LogEventIds.GameClient.Disconnected, Level = LogLevel.Information,
        Message = "Disconnected: {Reason}")]
    private partial void LogDisconnected(string reason);

    [LoggerMessage(EventId = LogEventIds.GameClient.EntitySpawned, Level = LogLevel.Debug,
        Message = "Entity spawned: {EntityId} ({EntityType})")]
    private partial void LogEntitySpawned(int entityId, string entityType);

    [LoggerMessage(EventId = LogEventIds.GameClient.EntityDestroyed, Level = LogLevel.Debug,
        Message = "Entity destroyed: {EntityId}")]
    private partial void LogEntityDestroyed(int entityId);

    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectionRejected, Level = LogLevel.Warning,
        Message = "Connection rejected: {Reason}")]
    private partial void LogConnectionRejected(string? reason);

    [LoggerMessage(EventId = LogEventIds.GameClient.ConnectionAccepted, Level = LogLevel.Information,
        Message = "Accepted. ClientId: {ClientId}, TickRate: {TickRate}")]
    private partial void LogConnectionAccepted(Guid clientId, int tickRate);

    [LoggerMessage(EventId = LogEventIds.GameClient.ClientBulkTransferComplete, Level = LogLevel.Information,
        Message = "Bulk transfer {TransferId} complete: {DataType} ({Size} bytes)")]
    private partial void LogClientBulkTransferComplete(Guid transferId, SandboxBulkDataType dataType, int size);

    [LoggerMessage(EventId = LogEventIds.GameClient.UnhandledNetMessage, Level = LogLevel.Debug,
        Message = "Unhandled inbound message: Id {MessageId} ({MessageType})")]
    private partial void LogUnhandledNetMessage(ushort messageId, string messageType);
}
