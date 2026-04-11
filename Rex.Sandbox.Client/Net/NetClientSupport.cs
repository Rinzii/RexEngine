using Microsoft.Extensions.Logging;
using Rex.Client.Net;
using Rex.Sandbox.Client.Input;
using Rex.Sandbox.Shared.Components;
using Rex.Sandbox.Shared.Components.Registration;
using Rex.Sandbox.Shared.Net;
using Rex.Sandbox.Shared.Net.Messages;
using Rex.Sandbox.Shared.Net.Transfer;
using Rex.Sandbox.Shared.Simulation;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Components.Registration;
using Rex.Shared.GameStates;
using Rex.Shared.Logging;
using Rex.Shared.Net;
using Rex.Shared.Net.Messages;
using Rex.Shared.Net.Replication;
using Rex.Shared.Net.Transfer;
using Rex.Shared.Serialization.Components;
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
    // One resync flight at a time while the tracker waits for a full baseline snapshot.
    private bool _awaitingFullState;

    public GameClient(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<GameClient>();
        Prediction = new PredictionSystem(InputBuffer);
        _transferManager = new BulkTransferManager(loggerFactory);
        _transferManager.TransferCompleted += OnTransferCompleted;
    }

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
            PlayerInputMessage input = _inputCollector.Sample(currentTick);
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
        // Drop session-scoped prediction and replicated state so the next connect starts clean.
        ClientId = Guid.Empty;
        LocalPlayerEntityId = 0;
        WorldState.Reset();
        Prediction.Reset();
        InputBuffer.Clear();
        _awaitingFullState = false;
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
                WorldState.ApplySpawn(spawn);
                LogEntitySpawned(spawn.EntityId, spawn.EntityType);
                break;
            case EntityDestroyMessage destroy:
                WorldState.ApplyDestroy(destroy);
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
        WorldState.Reset();
        Prediction.Reset();
        InputBuffer.Clear();
        _awaitingFullState = false;
        _channel!.State = ConnectionState.InGame;
        LogConnectionAccepted(response.ClientId, response.TickRate);
    }

    private void HandleWorldSnapshot(WorldSnapshotMessage snapshot)
    {
        GameStateApplyResult applyResult = WorldState.ApplySnapshot(snapshot);

        if (snapshot.IsFullSnapshot && applyResult == GameStateApplyResult.Applied)
        {
            _awaitingFullState = false;
        }

        if (WorldState.NeedsFullState)
        {
            // Partial deltas without a trusted baseline never get acked. Ask once for a full snapshot then wait.
            if (!_awaitingFullState && _channel != null)
            {
                RequestFullStateMessage request = new(WorldState.LastServerTick);
                _channel.Send(request);
                _awaitingFullState = true;
                LogFullStateRequested(WorldState.LastServerTick);
            }

            return;
        }

        if (applyResult != GameStateApplyResult.Applied)
        {
            return;
        }

        _channel?.Send(new StateAckMessage(snapshot.ServerTick));

        if (WorldState.TryGetEntityState(LocalPlayerEntityId, out EntityState entity))
        {
            // Server time for inputs already applied is the anchor for replaying local prediction ahead of it.
            Prediction.Reconcile(entity, snapshot.LastProcessedInputTick);
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
        TickRingBuffer<PlayerInputMessage?>.Entry slot = _buffer.GetSlot(input.Tick);
        slot.Tick = input.Tick;
        slot.IsAssigned = true;
        slot.Value = input;
    }

    public void AcknowledgeUpTo(uint tick)
    {
        for (int i = 0; i < _buffer.Capacity; i++)
        {
            TickRingBuffer<PlayerInputMessage?>.Entry slot = _buffer.GetSlotAt(i);
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

        for (int i = 0; i < _buffer.Capacity; i++)
        {
            TickRingBuffer<PlayerInputMessage?>.Entry slot = _buffer.GetSlotAt(i);
            if (slot is { IsAssigned: true, Value: not null } && slot.Tick > tick)
            {
                result.Add(slot.Value);
            }
        }

        result.Sort((a, b) => a.Tick.CompareTo(b.Tick));
        return result;
    }

    public void Clear()
    {
        // Clear every physical slot because ticks wrap and IsAssigned alone does not expose empties.
        for (int i = 0; i < _buffer.Capacity; i++)
        {
            TickRingBuffer<PlayerInputMessage?>.Entry slot = _buffer.GetSlotAt(i);
            slot.IsAssigned = false;
            slot.Value = null;
        }
    }
}

public sealed class PredictionSystem
{
    private readonly InputBuffer _inputBuffer;

    public PredictionSystem(InputBuffer inputBuffer)
    {
        _inputBuffer = inputBuffer;
    }

    public float PredictedX { get; private set; }
    public float PredictedY { get; private set; }
    public float PredictedZ { get; private set; }

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

        IReadOnlyList<PlayerInputMessage> unacknowledged = _inputBuffer.GetInputsAfter(lastProcessedInputTick);
        foreach (PlayerInputMessage input in unacknowledged)
        {
            ApplyInputLocally(input);
        }
    }

    public void Reset()
    {
        PredictedX = 0f;
        PredictedY = 0f;
        PredictedZ = 0f;
    }
}

public sealed class ClientWorldState
{
    private readonly AuthoritativeGameStateTracker<int, ReplicatedEntityState> _tracker = new(static entity => entity.EntityId);
    private readonly List<EntityState> _currentEntities = [];
    private readonly Dictionary<int, ReplicatedEntityState> _previousEntities = [];
    private readonly List<EntityState> _interpolatedEntities = [];
    private readonly int _transformComponentId;
    private readonly int _sandboxActorComponentId;

    public ClientWorldState()
    {
        ComponentRegistry registry = new();
        SharedEcsBootstrap.RegisterAll(registry);
        SandboxEcsBootstrap.RegisterAll(registry);
        _transformComponentId = registry.GetComponentId<TransformComponent>();
        _sandboxActorComponentId = registry.GetComponentId<SandboxActorComponent>();
    }

    public IGameState<ReplicatedEntityState>? CurrentSnapshot => _tracker.Current;
    public IReadOnlyList<EntityState> CurrentEntities => BuildCurrentEntities();
    public uint LastServerTick => _tracker.LastServerTick;
    public bool NeedsFullState => _tracker.NeedsFullState;

    public GameStateApplyResult ApplySnapshot(WorldSnapshotMessage snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _tracker.ApplySnapshot(snapshot);
    }

    public void ApplySpawn(EntitySpawnMessage spawn)
    {
        ArgumentNullException.ThrowIfNull(spawn);

        // Spawns are thin protocol rows. Rebuild the same replicated shape snapshots use so ApplyUpsert matches ApplySnapshot.
        List<ReplicatedComponentState> components =
        [
            new(_transformComponentId, ProtobufComponentSerializer<TransformComponent>.Instance.Serialize(new TransformComponent
            {
                X = spawn.X,
                Y = spawn.Y,
                Z = spawn.Z,
                RotationY = spawn.RotationY
            })),
            new(_sandboxActorComponentId, ProtobufComponentSerializer<SandboxActorComponent>.Instance.Serialize(new SandboxActorComponent
            {
                NetEntityId = spawn.EntityId,
                EntityType = spawn.EntityType
            }))
        ];

        ReplicatedEntityState upsert = new(spawn.EntityId, components);
        if (_tracker.TryGetCurrentEntity(spawn.EntityId, out ReplicatedEntityState existing))
        {
            upsert = MergeReplicatedEntityState(existing, upsert);
        }

        _tracker.ApplyUpsert(spawn.ServerTick, upsert);
    }

    public void ApplyDestroy(EntityDestroyMessage destroy)
    {
        ArgumentNullException.ThrowIfNull(destroy);
        _tracker.ApplyRemove(destroy.ServerTick, destroy.EntityId);
    }

    public void Reset()
    {
        _tracker.Reset();
    }

    public IReadOnlyList<EntityState> GetInterpolatedState(float alpha)
    {
        if (_tracker.Current == null)
        {
            return [];
        }

        _previousEntities.Clear();
        if (_tracker.Previous != null)
        {
            foreach (ReplicatedEntityState entity in _tracker.Previous.Entities)
            {
                _previousEntities[entity.EntityId] = entity;
            }
        }

        _interpolatedEntities.Clear();
        foreach (ReplicatedEntityState current in _tracker.Current.Entities)
        {
            if (!TryReadEntityState(current, out EntityState currentState))
            {
                continue;
            }

            if (_previousEntities.TryGetValue(current.EntityId, out ReplicatedEntityState? previous)
                && TryReadEntityState(previous, out EntityState previousState))
            {
                _interpolatedEntities.Add(EntityStateInterpolation.Lerp(previousState, currentState, alpha));
            }
            else
            {
                _interpolatedEntities.Add(currentState);
            }
        }

        return _interpolatedEntities;
    }

    private List<EntityState> BuildCurrentEntities()
    {
        _currentEntities.Clear();
        if (_tracker.Current == null)
        {
            return _currentEntities;
        }

        foreach (ReplicatedEntityState entity in _tracker.Current.Entities)
        {
            if (TryReadEntityState(entity, out EntityState entityState))
            {
                _currentEntities.Add(entityState);
            }
        }

        return _currentEntities;
    }

    public bool TryGetEntityState(int entityId, out EntityState entityState)
    {
        entityState = default!;
        if (_tracker.Current == null)
        {
            return false;
        }

        foreach (ReplicatedEntityState entity in _tracker.Current.Entities)
        {
            if (entity.EntityId == entityId && TryReadEntityState(entity, out entityState))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryReadEntityState(ReplicatedEntityState replicatedEntity, out EntityState entityState)
    {
        entityState = default!;
        // Legacy EntityState is still transform-only for render and prediction paths.
        for (int i = 0; i < replicatedEntity.Components.Count; i++)
        {
            ReplicatedComponentState component = replicatedEntity.Components[i];
            if (component.ComponentId != _transformComponentId)
            {
                continue;
            }

            TransformComponent transform = ProtobufComponentSerializer<TransformComponent>.Instance.Deserialize(component.Payload);
            entityState = new EntityState(
                replicatedEntity.EntityId,
                transform.X,
                transform.Y,
                transform.Z,
                transform.RotationY);
            return true;
        }

        return false;
    }

    private static ReplicatedEntityState MergeReplicatedEntityState(ReplicatedEntityState current, ReplicatedEntityState update)
    {
        Dictionary<int, ReplicatedComponentState> mergedComponents = [];
        for (int i = 0; i < current.Components.Count; i++)
        {
            ReplicatedComponentState component = current.Components[i];
            mergedComponents[component.ComponentId] = component;
        }

        for (int i = 0; i < update.Components.Count; i++)
        {
            ReplicatedComponentState component = update.Components[i];
            mergedComponents[component.ComponentId] = component;
        }

        return new ReplicatedEntityState(
            update.EntityId,
            [.. mergedComponents.OrderBy(static pair => pair.Key).Select(static pair => pair.Value)]);
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

    [LoggerMessage(EventId = LogEventIds.GameClient.FullStateRequested, Level = LogLevel.Information,
        Message = "Requested full authoritative state from tick {LastAppliedServerTick}")]
    private partial void LogFullStateRequested(uint lastAppliedServerTick);
}
