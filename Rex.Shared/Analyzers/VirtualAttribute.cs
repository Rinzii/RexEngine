namespace Rex.Shared.Analyzers;

/// <summary>Type exists for inheritance in analyzer contracts.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class VirtualAttribute : Attribute;
