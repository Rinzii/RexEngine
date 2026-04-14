using Rex.Shared.GameStates;

namespace Rex.Shared.Tests.GameStates;

public sealed class AuthoritativeGameStateTrackerTests
{
    [Fact]
    public void ApplySnapshot_merges_delta_frames_into_current_state()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);

        GameStateApplyResult first = tracker.ApplySnapshot(new DemoPartialGameState(
            1u,
            [new DemoEntityState(1, 10), new DemoEntityState(2, 20)],
            isFullSnapshot: true));
        GameStateApplyResult second = tracker.ApplySnapshot(new DemoPartialGameState(
            2u,
            [new DemoEntityState(1, 15)],
            isFullSnapshot: false));

        Assert.Equal(GameStateApplyResult.Applied, first);
        Assert.Equal(GameStateApplyResult.Applied, second);
        Assert.Collection(
            tracker.CurrentEntities,
            entity =>
            {
                Assert.Equal(1, entity.EntityId);
                Assert.Equal(15, entity.Value);
            },
            entity =>
            {
                Assert.Equal(2, entity.EntityId);
                Assert.Equal(20, entity.Value);
            });
    }

    [Fact]
    public void ApplySnapshot_delta_without_full_baseline_requests_resync()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);

        GameStateApplyResult result = tracker.ApplySnapshot(new DemoPartialGameState(
            1u,
            [new DemoEntityState(1, 10)],
            isFullSnapshot: false));

        Assert.Equal(GameStateApplyResult.MissingBaseline, result);
        Assert.True(tracker.NeedsFullState);
        Assert.Empty(tracker.CurrentEntities);
    }

    [Fact]
    public void ApplyUpsert_and_remove_refresh_the_current_frame_without_advancing_snapshot_tick()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);
        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            1u,
            [new DemoEntityState(1, 10)],
            isFullSnapshot: true));

        tracker.ApplyUpsert(2u, new DemoEntityState(2, 30));
        tracker.ApplyRemove(2u, 1);

        DemoEntityState remaining = Assert.Single(tracker.CurrentEntities);
        Assert.Equal(2, remaining.EntityId);
        Assert.Equal(30, remaining.Value);
        Assert.Equal(1u, tracker.LastServerTick);
    }

    [Fact]
    public void ApplyUpsert_and_remove_ignore_same_tick_side_channel_events()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);
        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            5u,
            [new DemoEntityState(1, 10)],
            isFullSnapshot: true));

        tracker.ApplyUpsert(5u, new DemoEntityState(2, 30));
        tracker.ApplyRemove(5u, 1);

        DemoEntityState remaining = Assert.Single(tracker.CurrentEntities);
        Assert.Equal(1, remaining.EntityId);
        Assert.Equal(10, remaining.Value);
    }

    [Fact]
    public void ApplyUpsert_buffers_future_side_channel_changes_across_intermediate_snapshots()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);
        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            5u,
            [new DemoEntityState(1, 10)],
            isFullSnapshot: true));

        tracker.ApplyUpsert(7u, new DemoEntityState(2, 30));

        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            6u,
            [new DemoEntityState(1, 15)],
            isFullSnapshot: false));

        Assert.Equal(6u, tracker.LastServerTick);
        Assert.Collection(
            tracker.CurrentEntities,
            entity =>
            {
                Assert.Equal(1, entity.EntityId);
                Assert.Equal(15, entity.Value);
            },
            entity =>
            {
                Assert.Equal(2, entity.EntityId);
                Assert.Equal(30, entity.Value);
            });
    }

    [Fact]
    public void ApplySnapshot_discards_buffered_side_channel_changes_when_authoritative_tick_catches_up()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);
        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            5u,
            [new DemoEntityState(1, 10)],
            isFullSnapshot: true));

        tracker.ApplyUpsert(7u, new DemoEntityState(2, 30));

        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            7u,
            [new DemoEntityState(1, 20)],
            isFullSnapshot: false));

        DemoEntityState entity = Assert.Single(tracker.CurrentEntities);
        Assert.Equal(1, entity.EntityId);
        Assert.Equal(20, entity.Value);
    }

    [Fact]
    public void ApplySnapshot_delta_removed_keys_retire_entities()
    {
        AuthoritativeGameStateTracker<int, DemoEntityState> tracker = new(static entity => entity.EntityId);
        _ = tracker.ApplySnapshot(new DemoPartialGameState(
            1u,
            [new DemoEntityState(1, 10), new DemoEntityState(2, 20)],
            isFullSnapshot: true));

        GameStateApplyResult result = tracker.ApplySnapshot(new DemoPartialGameState(
            2u,
            [],
            isFullSnapshot: false,
            removedKeys: [1]));

        Assert.Equal(GameStateApplyResult.Applied, result);
        DemoEntityState remaining = Assert.Single(tracker.CurrentEntities);
        Assert.Equal(2, remaining.EntityId);
    }

    private readonly record struct DemoEntityState(int EntityId, int Value);

    private sealed class DemoPartialGameState : IRemovablePartialGameState<int, DemoEntityState>
    {
        public DemoPartialGameState(uint serverTick, IReadOnlyList<DemoEntityState> entities, bool isFullSnapshot,
            IReadOnlyList<int>? removedKeys = null)
        {
            ServerTick = serverTick;
            Entities = entities;
            IsFullSnapshot = isFullSnapshot;
            RemovedKeys = removedKeys ?? [];
        }

        public uint ServerTick { get; }

        public IReadOnlyList<DemoEntityState> Entities { get; }

        public bool IsFullSnapshot { get; }

        public IReadOnlyList<int> RemovedKeys { get; }
    }
}
