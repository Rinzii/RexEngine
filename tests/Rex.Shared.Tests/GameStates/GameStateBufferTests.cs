using Rex.Shared.GameStates;

namespace Rex.Shared.Tests.GameStates;

public sealed class GameStateBufferTests
{
    [Fact]
    public void Apply_tracks_previous_and_current_frames()
    {
        GameStateBuffer<DemoEntityState> buffer = new();
        DemoGameState first = new(1, [new DemoEntityState(1, 10)]);
        DemoGameState second = new(2, [new DemoEntityState(1, 20)]);

        buffer.Apply(first);
        buffer.Apply(second);

        Assert.Same(first, buffer.Previous);
        Assert.Same(second, buffer.Current);
        Assert.Equal(2u, buffer.LastServerTick);
    }

    [Fact]
    public void Interpolate_merges_matching_entities_from_previous_frame()
    {
        GameStateBuffer<DemoEntityState> buffer = new();
        buffer.Apply(new DemoGameState(1, [new DemoEntityState(1, 10), new DemoEntityState(2, 30)]));
        buffer.Apply(new DemoGameState(2, [new DemoEntityState(1, 20), new DemoEntityState(3, 90)]));

        IReadOnlyList<DemoEntityState> result = GameStateInterpolation.Interpolate(
            buffer,
            0.5f,
            static entity => entity.Id,
            static (previous, current, alpha) => new DemoEntityState(
                current.Id,
                previous.Value + ((current.Value - previous.Value) * alpha)));

        Assert.Collection(
            result,
            entity =>
            {
                Assert.Equal(1, entity.Id);
                Assert.Equal(15f, entity.Value);
            },
            entity =>
            {
                Assert.Equal(3, entity.Id);
                Assert.Equal(90f, entity.Value);
            });
    }

    [Fact]
    public void ReplaceCurrent_keeps_previous_frame_unchanged()
    {
        GameStateBuffer<DemoEntityState> buffer = new();
        DemoGameState first = new(1, [new DemoEntityState(1, 10)]);
        DemoGameState second = new(2, [new DemoEntityState(1, 20)]);
        DemoGameState replacement = new(2, [new DemoEntityState(1, 25)]);

        buffer.Apply(first);
        buffer.Apply(second);
        buffer.ReplaceCurrent(replacement);

        Assert.Same(first, buffer.Previous);
        Assert.Same(replacement, buffer.Current);
    }

    [Fact]
    public void Clear_resets_previous_and_current_frames()
    {
        GameStateBuffer<DemoEntityState> buffer = new();
        buffer.Apply(new DemoGameState(1, [new DemoEntityState(1, 10)]));

        buffer.Clear();

        Assert.Null(buffer.Previous);
        Assert.Null(buffer.Current);
        Assert.Equal(0u, buffer.LastServerTick);
    }

    private sealed class DemoGameState : IGameState<DemoEntityState>
    {
        public DemoGameState(uint serverTick, IReadOnlyList<DemoEntityState> entities)
        {
            ServerTick = serverTick;
            Entities = entities;
        }

        public uint ServerTick { get; }
        public IReadOnlyList<DemoEntityState> Entities { get; }
    }

    private readonly record struct DemoEntityState(int Id, float Value);
}
