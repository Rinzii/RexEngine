namespace Rex.Shared.Analyzers;

/// <summary>
///     Derived types must carry <see cref="SerializableAttribute"/> or the analyzer warns about missing serialization markers.
/// </summary>
/// <example>
/// <code>
///     [RequiresSerializable]
///     public abstract MyParent;
///     <br/>
///     // Warning RA0001: Class not marked as (Net)Serializable.
///     public sealed class MyChild1 : MyParent;
///     <br/>
///     // No warning.
///     [NetSerializable, Serializable]
///     public sealed class MyChild2 : MyParent;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresSerializableAttribute : Attribute;
