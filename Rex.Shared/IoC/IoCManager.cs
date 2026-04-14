using System.Reflection;

namespace Rex.Shared.IoC;

/// <summary>
/// Minimal static IoC container with singleton registration, resolution and field injection.
/// </summary>
public static class IoCManager
{
    private static readonly Lock s_sync = new();
    private static readonly Dictionary<Type, Registration> s_registrations = [];
    private static readonly Dictionary<Type, object> s_singletonsByImplementation = [];
    private static bool s_graphBuilt;

    /// <summary>
    /// Registers a singleton service mapping.
    /// </summary>
    /// <typeparam name="TInterface">Service type to resolve.</typeparam>
    /// <typeparam name="TImplementation">Concrete implementation type.</typeparam>
    /// <param name="overwrite">Whether an existing registration may be replaced.</param>
    public static void Register<TInterface, TImplementation>(bool overwrite = false)
        where TInterface : class
        where TImplementation : class, TInterface, new()
    {
        Register(typeof(TInterface), typeof(TImplementation), overwrite);
    }

    /// <summary>
    /// Registers a singleton service mapping.
    /// </summary>
    /// <param name="serviceType">Service type to resolve.</param>
    /// <param name="implementationType">Concrete implementation type.</param>
    /// <param name="overwrite">Whether an existing registration may be replaced.</param>
    public static void Register(Type serviceType, Type implementationType, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new InvalidOperationException(
                $"Implementation type '{implementationType.FullName}' does not implement '{serviceType.FullName}'.");
        }

        if (implementationType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException(
                $"Implementation type '{implementationType.FullName}' must have a public parameterless constructor.");
        }

        lock (s_sync)
        {
            if (s_registrations.ContainsKey(serviceType) && !overwrite)
            {
                throw new InvalidOperationException(
                    $"Service type '{serviceType.FullName}' is already registered.");
            }

            s_registrations[serviceType] = new Registration(serviceType, implementationType, null);
            s_graphBuilt = false;
        }
    }

    /// <summary>
    /// Registers an existing singleton instance for a service type.
    /// </summary>
    /// <typeparam name="TInterface">Service type to resolve.</typeparam>
    /// <param name="instance">Existing singleton instance.</param>
    /// <param name="overwrite">Whether an existing registration may be replaced.</param>
    public static void RegisterInstance<TInterface>(TInterface instance, bool overwrite = false)
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        RegisterInstance(typeof(TInterface), instance, overwrite);
    }

    /// <summary>
    /// Registers an existing singleton instance for a service type.
    /// </summary>
    /// <param name="serviceType">Service type to resolve.</param>
    /// <param name="instance">Existing singleton instance.</param>
    /// <param name="overwrite">Whether an existing registration may be replaced.</param>
    public static void RegisterInstance(Type serviceType, object instance, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(instance);

        Type implementationType = instance.GetType();
        if (!serviceType.IsAssignableFrom(implementationType))
        {
            throw new InvalidOperationException(
                $"Implementation type '{implementationType.FullName}' does not implement '{serviceType.FullName}'.");
        }

        lock (s_sync)
        {
            if (s_registrations.ContainsKey(serviceType) && !overwrite)
            {
                throw new InvalidOperationException(
                    $"Service type '{serviceType.FullName}' is already registered.");
            }

            s_registrations[serviceType] = new Registration(serviceType, implementationType, instance);
            s_singletonsByImplementation[implementationType] = instance;
            s_graphBuilt = false;
        }
    }

    /// <summary>
    /// Resolves a singleton service instance.
    /// </summary>
    /// <typeparam name="TInterface">Service type to resolve.</typeparam>
    /// <returns>The shared singleton instance.</returns>
    public static TInterface Resolve<TInterface>()
        where TInterface : class
    {
        return (TInterface)ResolveType(typeof(TInterface));
    }

    /// <summary>
    /// Resolves a singleton service instance.
    /// </summary>
    /// <param name="serviceType">Service type to resolve.</param>
    /// <returns>The shared singleton instance.</returns>
    public static object ResolveType(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        lock (s_sync)
        {
            EnsureGraphBuilt();

            if (!s_registrations.TryGetValue(serviceType, out Registration? registration))
            {
                throw new InvalidOperationException(
                    $"Service type '{serviceType.FullName}' has not been registered.");
            }

            return registration.Instance
                ?? throw new InvalidOperationException(
                    $"Service type '{serviceType.FullName}' was registered without an initialized singleton.");
        }
    }

    /// <summary>
    /// Injects all <see cref="DependencyAttribute"/> fields on an instance.
    /// </summary>
    /// <param name="instance">Instance to inject.</param>
    public static void InjectDependencies(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        lock (s_sync)
        {
            EnsureGraphBuilt();
            InjectDependenciesInternal(instance, callPostInject: true);
        }
    }

    /// <summary>
    /// Clears all registrations and singleton instances. Intended for tests and isolated bootstrap paths.
    /// </summary>
    public static void Clear()
    {
        lock (s_sync)
        {
            s_registrations.Clear();
            s_singletonsByImplementation.Clear();
            s_graphBuilt = false;
        }
    }

    private static void EnsureGraphBuilt()
    {
        if (s_graphBuilt)
        {
            return;
        }

        // Construct every implementation once then bind each service key before field injection runs.
        foreach (Registration registration in s_registrations.Values)
        {
            if (registration.Instance != null)
            {
                s_singletonsByImplementation[registration.ImplementationType] = registration.Instance;
                continue;
            }

            if (!s_singletonsByImplementation.ContainsKey(registration.ImplementationType))
            {
                object instance = Activator.CreateInstance(registration.ImplementationType)
                    ?? throw new InvalidOperationException(
                        $"Failed to construct service type '{registration.ImplementationType.FullName}'.");
                s_singletonsByImplementation[registration.ImplementationType] = instance;
            }
        }

        foreach ((Type serviceType, Registration registration) in s_registrations.ToArray())
        {
            object instance = s_singletonsByImplementation[registration.ImplementationType];
            s_registrations[serviceType] = registration with { Instance = instance };
        }

        foreach (object singleton in s_singletonsByImplementation.Values)
        {
            InjectDependenciesInternal(singleton, callPostInject: false);
        }

        // PostInject runs last so handlers can touch other services after dependency fields are wired.
        foreach (object singleton in s_singletonsByImplementation.Values)
        {
            if (singleton is IPostInjectInit postInject)
            {
                postInject.PostInject();
            }
        }

        s_graphBuilt = true;
    }

    private static void InjectDependenciesInternal(object instance, bool callPostInject)
    {
        Type? currentType = instance.GetType();
        while (currentType != null)
        {
            FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in fields)
            {
                if (!field.IsDefined(typeof(DependencyAttribute), inherit: true))
                {
                    continue;
                }

                object dependency = ResolveRegisteredDependency(field.FieldType);
                field.SetValue(instance, dependency);
            }

            currentType = currentType.BaseType;
        }

        if (callPostInject && instance is IPostInjectInit postInject)
        {
            postInject.PostInject();
        }
    }

    private static object ResolveRegisteredDependency(Type serviceType)
    {
        if (!s_registrations.TryGetValue(serviceType, out Registration? registration))
        {
            throw new InvalidOperationException(
                $"Dependency field requested unregistered service type '{serviceType.FullName}'.");
        }

        return registration.Instance
            ?? throw new InvalidOperationException(
                $"Dependency field requested service type '{serviceType.FullName}' before it was initialized.");
    }

    private sealed record Registration(Type ServiceType, Type ImplementationType, object? Instance);
}
