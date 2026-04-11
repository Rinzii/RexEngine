namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a data field so composed mappings always push the inherited value forward.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class AlwaysPushInheritanceAttribute : Attribute;
