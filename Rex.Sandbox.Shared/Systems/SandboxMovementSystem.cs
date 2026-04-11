using Rex.Sandbox.Shared.Components;
using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Entities;
using Rex.Shared.GameObjects;

namespace Rex.Sandbox.Shared.Systems;

/// <summary>
/// Applies Sandbox input events to transform state.
/// </summary>
public sealed class SandboxMovementSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeReadOnlyLocalEvent<SandboxMoverComponent, SandboxPlayerInputEvent>(OnPlayerInput);
    }

    private void OnPlayerInput(EntityId entity, in SandboxMoverComponent mover, ref SandboxPlayerInputEvent args)
    {
        if (!World.Has<TransformComponent>(entity))
        {
            return;
        }

        ref TransformComponent transform = ref World.GetMutableRef<TransformComponent>(entity);
        transform.X = MathF.FusedMultiplyAdd(args.MoveX, mover.PlanarUnitsPerInputTick, transform.X);
        transform.Z = MathF.FusedMultiplyAdd(args.MoveY, mover.PlanarUnitsPerInputTick, transform.Z);
        transform.RotationY = args.LookY;
    }
}
