using System;

namespace Rex.Shared.GameObjects;

/// <summary>Marks an event struct as passed by ref to RaiseLocalEvent style APIs. Analyzers only today.</summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ByRefEventAttribute : Attribute;
