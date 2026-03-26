using System;

namespace Rex.Shared.GameObjects;

/// <summary>
/// Marks an event type as raised by-ref (used with ByRef event analyzer metadata).
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ByRefEventAttribute : Attribute;
