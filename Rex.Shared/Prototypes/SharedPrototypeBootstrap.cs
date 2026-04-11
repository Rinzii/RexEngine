namespace Rex.Shared.Prototypes;

/// <summary>
/// Registers shared prototype types that ship with this assembly.
/// </summary>
public static class SharedPrototypeBootstrap
{
    /// <summary>
    /// Registers each shared prototype type.
    /// </summary>
    /// <param name="prototypeManager">Prototype manager to populate.</param>
    public static void RegisterAll(PrototypeManager prototypeManager)
    {
        ArgumentNullException.ThrowIfNull(prototypeManager);
        prototypeManager.RegisterPrototype<EntityPrototype>();
        prototypeManager.RegisterPrototype<MapPrototype>();
        prototypeManager.RegisterPrototype<ModelPrototype>();
        prototypeManager.RegisterPrototype<ScenePrototype>();
        prototypeManager.RegisterPrototype<PrototypeCategoryPrototype>();
    }
}
