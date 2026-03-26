using System;

namespace Rex.Shared.Serialization;

/// <summary>
/// Marks a type for network serialization with NetSerializer. Pair it with <see cref="SerializableAttribute"/>.
/// Use <see cref="NonSerializedAttribute"/> on fields to skip them. Derived types inherit the contract.
/// See <see href="https://github.com/tomba/netserializer/blob/master/Doc.md">NetSerializer documentation</see> for protocol details.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, AllowMultiple = false,
    Inherited = false)]
public sealed class NetSerializableAttribute : Attribute
{
}