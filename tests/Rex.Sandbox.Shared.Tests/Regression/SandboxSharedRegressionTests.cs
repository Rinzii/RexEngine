using Rex.Sandbox.Shared.Simulation;

namespace Rex.Sandbox.Shared.Tests.Regression;

// Locks small Sandbox shared behaviors that the consumer should keep owning.
public sealed class SandboxSharedRegressionTests
{
    [Fact]
    public void Regression_game_world_spawn_ids_are_monotonic()
    {
        GameWorld world = new();
        int a = world.SpawnEntity(Guid.Empty, "T", 0f, 0f, 0f);
        int b = world.SpawnEntity(Guid.Empty, "T", 0f, 0f, 0f);

        Assert.Equal(1, a);
        Assert.Equal(2, b);
    }
}
