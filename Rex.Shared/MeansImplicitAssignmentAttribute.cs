using System;

namespace Rex.Shared;

/// <summary>Field is set by codegen or the engine. Suppresses missing-assignment noise where supported.</summary>
public sealed class MeansImplicitAssignmentAttribute : Attribute
{
}