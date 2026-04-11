namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a type as participating in reflection-based data definition serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class DataDefinitionAttribute : Attribute;
