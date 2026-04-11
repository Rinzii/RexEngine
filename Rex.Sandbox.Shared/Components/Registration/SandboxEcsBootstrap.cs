using Rex.Shared.Components;
using Rex.Shared.Components.Registration;
using Rex.Shared.Serialization.Components;

namespace Rex.Sandbox.Shared.Components.Registration;

/// <summary>
/// Registers ECS components that belong to the Sandbox sample.
/// </summary>
public static class SandboxEcsBootstrap
{
    /// <summary>Stable shared id for <see cref="SandboxActorComponent"/>.</summary>
    public const int SandboxActorComponentId = 2000;

    /// <summary>Stable shared id for <see cref="SandboxMoverComponent"/>.</summary>
    public const int SandboxMoverComponentId = 2001;

    /// <summary>Stable shared id for <see cref="SandboxModelComponent"/>.</summary>
    public const int SandboxModelComponentId = 2002;

    /// <summary>Registers the sandbox ECS component set.</summary>
    /// <param name="registry">Registry to populate.</param>
    public static void RegisterAll(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        RegisterIfNeeded<SandboxActorComponent>(registry, SandboxActorComponentId, "sandboxActor");
        RegisterIfNeeded<SandboxMoverComponent>(registry, SandboxMoverComponentId, "sandboxMover");
        RegisterIfNeeded<SandboxModelComponent>(registry, SandboxModelComponentId, "sandboxModel");
    }

    private static void RegisterIfNeeded<T>(ComponentRegistry registry, int componentId, string componentName)
        where T : struct, IComponent
    {
        if (registry.IsRegistered<T>())
        {
            if (registry.GetComponentId<T>() != componentId || registry.GetComponentName<T>() != componentName)
            {
                throw new InvalidOperationException(
                    $"Component '{typeof(T).FullName}' is already registered with a different identity.");
            }

            return;
        }

        registry.Register(componentId, componentName, ProtobufComponentSerializer<T>.Instance);
    }
}
