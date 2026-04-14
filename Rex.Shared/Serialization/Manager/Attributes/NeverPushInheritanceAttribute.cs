namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a data field so composed mappings do not push inherited values forward.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class NeverPushInheritanceAttribute : Attribute;
