using System;

namespace Rex.Shared.Serialization
{
    /// <summary>
    /// This attribute marks an object as able to be serialized by the game's NetSerializer. It is required that objects
    /// that have this Attribute also have the <see cref="SerializableAttribute"/>. You can use
    /// <see cref="NonSerializedAttribute"/> to mark a field as non-serialized. Child classes are also NetSerializable.
    /// See the <see href="https://github.com/tomba/netserializer/blob/master/Doc.md">NetSerializer Documentation</see>
    /// for more info.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public sealed class NetSerializableAttribute : Attribute
    {
    }
}
