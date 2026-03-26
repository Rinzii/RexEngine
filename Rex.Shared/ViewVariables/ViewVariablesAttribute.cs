using System;
using Rex.Shared.Serialization;

namespace Rex.Shared.ViewVariables
{
    /// <summary>Marks a member for view-variables style inspection tools.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ViewVariablesAttribute : Attribute
    {
        public readonly VVAccess Access = VVAccess.ReadOnly;

        public ViewVariablesAttribute()
        {

        }

        /// <param name="access">Whether remote tools may write the value.</param>
        public ViewVariablesAttribute(VVAccess access)
        {
            Access = access;
        }
    }

    [Serializable, NetSerializable]
    public enum VVAccess : byte
    {
        /// <summary>Inspect only.</summary>
        ReadOnly = 0,

        /// <summary>Read and write allowed.</summary>
        ReadWrite = 1,
    }
}
