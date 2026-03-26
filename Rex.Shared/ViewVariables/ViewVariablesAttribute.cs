using System;
using Rex.Shared.Serialization;

namespace Rex.Shared.ViewVariables
{
    /// <summary>
    ///     Attribute to make a property or field accessible to VV.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public sealed class ViewVariablesAttribute : Attribute
    {
        public readonly VVAccess Access = VVAccess.ReadOnly;

        public ViewVariablesAttribute()
        {

        }

        public ViewVariablesAttribute(VVAccess access)
        {
            Access = access;
        }
    }

    [Serializable, NetSerializable]
    public enum VVAccess : byte
    {
        /// <summary>
        ///     This property can only be read, not written.
        /// </summary>
        ReadOnly = 0,

        /// <summary>
        ///     This property is read and writable.
        /// </summary>
        ReadWrite = 1,
    }
}
