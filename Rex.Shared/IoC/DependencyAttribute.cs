using System;

namespace Rex.Shared.IoC
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DependencyAttribute : Attribute
    {
    }
}
