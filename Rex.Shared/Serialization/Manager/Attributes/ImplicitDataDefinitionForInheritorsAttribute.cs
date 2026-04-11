namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a base type or interface so all inheritors are treated as data definitions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public sealed class ImplicitDataDefinitionForInheritorsAttribute : Attribute;
