using Rex.Shared.Components.BuiltIn;
using Rex.Shared.Serialization.Components;

namespace Rex.Shared.Components.Registration;

/// <summary>
/// Registers the shared ECS components and serializers that ship with this assembly.
/// </summary>
public static class SharedEcsBootstrap
{
    /// <summary>Stable shared id for <see cref="TransformComponent"/>.</summary>
    public const int TransformComponentId = 1000;

    /// <summary>Stable shared id for <see cref="OwnerComponent"/>.</summary>
    public const int OwnerComponentId = 1001;

    /// <summary>Stable shared id for <see cref="MetaDataComponent"/>.</summary>
    public const int MetaDataComponentId = 1002;

    /// <summary>Registers the shared ECS component set.</summary>
    /// <param name="registry">Registry to populate.</param>
    public static void RegisterAll(ComponentRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        RegisterIfNeeded<TransformComponent>(registry, TransformComponentId, "transform");
        RegisterIfNeeded<OwnerComponent>(registry, OwnerComponentId, "owner");
        RegisterIfNeeded<MetaDataComponent>(registry, MetaDataComponentId, "metadata");
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
