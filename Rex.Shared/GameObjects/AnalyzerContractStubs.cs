namespace Rex.Shared.GameObjects;

/// <summary>Marks event types used with ref parameters on local event dispatch APIs.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ByRefEventAttribute : Attribute;
