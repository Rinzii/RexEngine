namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks an attribute type so annotated types are treated as data definitions.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MeansDataDefinitionAttribute : Attribute;
