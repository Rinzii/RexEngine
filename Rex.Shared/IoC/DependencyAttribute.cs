using System;

namespace Rex.Shared.IoC;

/// <summary>Marks a field filled by the DI container. Do not assign in user code (analyzer warns).</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class DependencyAttribute : Attribute
{
}
