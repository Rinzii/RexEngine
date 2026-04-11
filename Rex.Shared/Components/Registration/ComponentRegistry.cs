using Rex.Shared.Entities.Storage;
using Rex.Shared.Serialization.Components;

namespace Rex.Shared.Components.Registration;

/// <summary>
/// Registers component types with stable ids, names, storage factories and serializers.
/// A registry is frozen before the first world uses it.
/// </summary>
public sealed class ComponentRegistry
{
    private readonly Dictionary<int, ComponentRegistration> _byId = [];
    private readonly Dictionary<string, ComponentRegistration> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<Type, ComponentRegistration> _byType = [];

    /// <summary>Gets the number of registered component types.</summary>
    public int Count => _byId.Count;

    /// <summary>Gets a value indicating whether registration has been frozen for world use.</summary>
    public bool IsFrozen { get; private set; }

    /// <summary>Registers one component type with its stable identity and serializer.</summary>
    /// <param name="componentId">Stable shared component id.</param>
    /// <param name="componentName">Stable shared component name.</param>
    /// <param name="serializer">Serializer for the component payload.</param>
    /// <typeparam name="T">Component type being registered.</typeparam>
    public void Register<T>(int componentId, string componentName, IComponentSerializer<T> serializer)
        where T : struct, IComponent
    {
        ThrowIfFrozen();

        if (componentId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentId), componentId, "Component ids must be positive.");
        }

        if (string.IsNullOrWhiteSpace(componentName))
        {
            throw new ArgumentException("Component name must not be null or whitespace.", nameof(componentName));
        }

        ArgumentNullException.ThrowIfNull(serializer);

        Type componentType = typeof(T);
        if (_byId.ContainsKey(componentId))
        {
            throw new InvalidOperationException($"Component id {componentId} is already registered.");
        }

        if (_byName.ContainsKey(componentName))
        {
            throw new InvalidOperationException($"Component name '{componentName}' is already registered.");
        }

        if (_byType.ContainsKey(componentType))
        {
            throw new InvalidOperationException(
                $"Component type '{componentType.FullName}' is already registered.");
        }

        var registration = ComponentRegistration.Create(componentId, componentName, serializer);
        _byId.Add(componentId, registration);
        _byName.Add(componentName, registration);
        _byType.Add(componentType, registration);
    }

    /// <summary>Prevents any further component registrations.</summary>
    public void Freeze()
    {
        IsFrozen = true;
    }

    /// <summary>Checks whether a component type has been registered.</summary>
    /// <typeparam name="T">Component type to test.</typeparam>
    /// <returns><see langword="true"/> when the component is registered.</returns>
    public bool IsRegistered<T>()
        where T : struct, IComponent
    {
        return _byType.ContainsKey(typeof(T));
    }

    /// <summary>Checks whether a runtime type has been registered as a component.</summary>
    /// <param name="componentType">Component type to test.</param>
    /// <returns><see langword="true"/> when the component is registered.</returns>
    public bool IsRegistered(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        return _byType.ContainsKey(componentType);
    }

    /// <summary>Gets the stable id for a registered component type.</summary>
    /// <typeparam name="T">Registered component type.</typeparam>
    /// <returns>The stable component id.</returns>
    public int GetComponentId<T>()
        where T : struct, IComponent
    {
        return GetRegistration(typeof(T)).Id;
    }

    /// <summary>Gets the stable name for a registered component type.</summary>
    /// <typeparam name="T">Registered component type.</typeparam>
    /// <returns>The stable component name.</returns>
    public string GetComponentName<T>()
        where T : struct, IComponent
    {
        return GetRegistration(typeof(T)).Name;
    }

    /// <summary>Gets the registered component CLR type for a stable component id.</summary>
    /// <param name="componentId">Stable component id.</param>
    /// <returns>The registered CLR type.</returns>
    public Type GetComponentType(int componentId)
    {
        return GetRegistration(componentId).ComponentType;
    }

    /// <summary>Attempts to resolve a registered CLR type from a stable component name.</summary>
    /// <param name="componentName">Stable component name.</param>
    /// <param name="componentType">Resolved CLR type when registered.</param>
    /// <returns><see langword="true"/> when the name is registered.</returns>
    public bool TryGetComponentType(string componentName, out Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentName);
        if (_byName.TryGetValue(componentName, out ComponentRegistration? registration))
        {
            componentType = registration.ComponentType;
            return true;
        }

        componentType = null!;
        return false;
    }

    /// <summary>Gets the stable component name for a registered runtime type.</summary>
    /// <param name="componentType">Registered component type.</param>
    /// <returns>The stable component name.</returns>
    public string GetComponentName(Type componentType)
    {
        return GetRegistration(componentType).Name;
    }

    /// <summary>Attempts to resolve a stable component id from a CLR type.</summary>
    /// <param name="componentType">Registered component type.</param>
    /// <param name="componentId">Resolved stable component id.</param>
    /// <returns><see langword="true"/> when the type is registered.</returns>
    public bool TryGetComponentId(Type componentType, out int componentId)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (_byType.TryGetValue(componentType, out ComponentRegistration? registration))
        {
            componentId = registration.Id;
            return true;
        }

        componentId = default;
        return false;
    }

    internal ComponentRegistration GetRegistration(int componentId)
    {
        if (!_byId.TryGetValue(componentId, out ComponentRegistration? registration))
        {
            throw new InvalidOperationException($"Component id {componentId} is not registered.");
        }

        return registration;
    }

    internal ComponentRegistration GetRegistration(Type componentType)
    {
        ArgumentNullException.ThrowIfNull(componentType);
        if (!_byType.TryGetValue(componentType, out ComponentRegistration? registration))
        {
            throw new InvalidOperationException($"Component type '{componentType.FullName}' is not registered.");
        }

        return registration;
    }

    internal ComponentRegistration GetRegistration<T>()
        where T : struct, IComponent
    {
        return GetRegistration(typeof(T));
    }

    internal bool TryGetRegistration(int componentId, out ComponentRegistration registration)
    {
        return _byId.TryGetValue(componentId, out registration!);
    }

    private void ThrowIfFrozen()
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException("Component registration is frozen.");
        }
    }
}

internal sealed class ComponentRegistration
{
    private readonly Func<IComponentColumn> _createColumn;

    private ComponentRegistration(
        int id,
        string name,
        Type componentType,
        IUntypedComponentSerializer serializer,
        Func<IComponentColumn> createColumn)
    {
        Id = id;
        Name = name;
        ComponentType = componentType;
        Serializer = serializer;
        _createColumn = createColumn;
    }

    public int Id { get; }

    public string Name { get; }

    public Type ComponentType { get; }

    public IUntypedComponentSerializer Serializer { get; }

    public IComponentColumn CreateColumn() => _createColumn();

    public static ComponentRegistration Create<T>(int id, string name, IComponentSerializer<T> serializer)
        where T : struct, IComponent
    {
        return new ComponentRegistration(
            id,
            name,
            typeof(T),
            new UntypedComponentSerializer<T>(serializer),
            static () => new ComponentColumn<T>());
    }
}

internal interface IUntypedComponentSerializer
{
    byte[] SerializeBoxed(object component);

    object DeserializeBoxed(ReadOnlyMemory<byte> payload);
}

internal sealed class UntypedComponentSerializer<T> : IUntypedComponentSerializer
    where T : struct, IComponent
{
    private readonly IComponentSerializer<T> _serializer;

    public UntypedComponentSerializer(IComponentSerializer<T> serializer)
    {
        _serializer = serializer;
    }

    public byte[] SerializeBoxed(object component)
    {
        if (component is not T typed)
        {
            throw new InvalidOperationException(
                $"Cannot serialize component value of type '{component.GetType().FullName}' as '{typeof(T).FullName}'.");
        }

        return _serializer.Serialize(typed);
    }

    public object DeserializeBoxed(ReadOnlyMemory<byte> payload) => _serializer.Deserialize(payload);
}
