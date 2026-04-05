using System;
using Rex.Shared.Serialization;

namespace Rex.Shared.ViewVariables;

/// <summary>Marks a member for inspection tools that use the view variable pattern.</summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class ViewVariablesAttribute : Attribute
{
    /// <summary>Default access granted to remote tools.</summary>
    public readonly VVAccess Access = VVAccess.ReadOnly;

    /// <summary>Uses <see cref="VVAccess.ReadOnly"/>.</summary>
    public ViewVariablesAttribute()
    {
    }

    /// <param name="access">Whether remote tools may write the value.</param>
    public ViewVariablesAttribute(VVAccess access)
    {
        Access = access;
    }
}

/// <summary>Access mode for view variable tooling.</summary>
[Serializable]
[NetSerializable]
// ReSharper disable once InconsistentNaming
public enum VVAccess : byte
{
    /// <summary>Inspect only.</summary>
    ReadOnly = 0,

    /// <summary>Read and write allowed.</summary>
    ReadWrite = 1
}
