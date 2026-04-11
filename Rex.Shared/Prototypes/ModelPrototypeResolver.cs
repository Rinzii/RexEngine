using Rex.Shared.Assets.Rdm;

namespace Rex.Shared.Prototypes;

/// <summary>
/// Resolves shared model prototypes into authored RDM packages and package-local selections.
/// </summary>
public sealed class ModelPrototypeResolver
{
    private readonly PrototypeManager _prototypeManager;
    private readonly RdmCatalog _rdmCatalog;

    /// <summary>
    /// Creates one model prototype resolver.
    /// </summary>
    /// <param name="prototypeManager">Prototype manager used to resolve shared model prototypes.</param>
    /// <param name="rdmCatalog">Catalog used to resolve authored RDM packages.</param>
    public ModelPrototypeResolver(PrototypeManager prototypeManager, RdmCatalog rdmCatalog)
    {
        _prototypeManager = prototypeManager ?? throw new ArgumentNullException(nameof(prototypeManager));
        _rdmCatalog = rdmCatalog ?? throw new ArgumentNullException(nameof(rdmCatalog));
    }

    /// <summary>
    /// Resolves one shared model prototype id.
    /// </summary>
    /// <param name="prototypeId">Shared model prototype id.</param>
    /// <returns>Resolved model prototype information.</returns>
    public ResolvedModelPrototype Resolve(string prototypeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prototypeId);

        ModelPrototype prototype = _prototypeManager.Index<ModelPrototype>(prototypeId);
        RdmPackage package = _rdmCatalog.Index(prototype.Rdm);

        RdmPrototypeDefinition? packagePrototype = null;
        if (!string.IsNullOrWhiteSpace(prototype.PackagePrototype))
        {
            packagePrototype = package.Definition.Prototypes
                .FirstOrDefault(
                    candidate => string.Equals(candidate.Name, prototype.PackagePrototype, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Model prototype '{prototype.Id}' references missing RDM package prototype '{prototype.PackagePrototype}'.");
        }

        string? defaultState = prototype.DefaultState;
        if (string.IsNullOrWhiteSpace(defaultState))
        {
            defaultState = packagePrototype?.DefaultState;
        }

        if (!string.IsNullOrWhiteSpace(defaultState)
            && !package.Definition.States.Any(state => string.Equals(state.Name, defaultState, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Model prototype '{prototype.Id}' resolved default state '{defaultState}' that does not exist in '{prototype.Rdm}'.");
        }

        return new ResolvedModelPrototype(prototype, package, packagePrototype, defaultState);
    }
}

/// <summary>
/// Resolved bridge between one shared model prototype and one authored RDM package.
/// </summary>
public sealed class ResolvedModelPrototype
{
    /// <summary>
    /// Creates one resolved model prototype.
    /// </summary>
    /// <param name="prototype">Resolved shared model prototype.</param>
    /// <param name="package">Resolved RDM package.</param>
    /// <param name="packagePrototype">Resolved package-local prototype when one was selected.</param>
    /// <param name="defaultState">Resolved default state when one was selected.</param>
    public ResolvedModelPrototype(
        ModelPrototype prototype,
        RdmPackage package,
        RdmPrototypeDefinition? packagePrototype,
        string? defaultState)
    {
        Prototype = prototype ?? throw new ArgumentNullException(nameof(prototype));
        Package = package ?? throw new ArgumentNullException(nameof(package));
        PackagePrototype = packagePrototype;
        DefaultState = defaultState;
    }

    /// <summary>Gets the resolved shared model prototype.</summary>
    public ModelPrototype Prototype { get; }

    /// <summary>Gets the resolved authored package.</summary>
    public RdmPackage Package { get; }

    /// <summary>Gets the selected package-local RDM prototype when present.</summary>
    public RdmPrototypeDefinition? PackagePrototype { get; }

    /// <summary>Gets the resolved default state when present.</summary>
    public string? DefaultState { get; }
}
