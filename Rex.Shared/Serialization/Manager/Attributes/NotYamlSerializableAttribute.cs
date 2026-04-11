namespace Rex.Shared.Serialization.Manager.Attributes;

/// <summary>
/// Marks a type as excluded from reflection-based data definition serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum)]
public sealed class NotYamlSerializableAttribute : Attribute;
