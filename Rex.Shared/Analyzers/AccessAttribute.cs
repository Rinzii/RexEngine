#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
/// <para>
///     Field, method and property permissions finer than plain public or private. Same idea as C++ friend classes.
/// </para>
/// <para>
///     Three roles. Self is the declaring type. Friend lists explicit types on this attribute. Other is everyone else.
///     Pick allowed operations per role with <see cref="AccessPermissions"/>.
/// </para>
/// </summary>
/// <example>
/// <code>
/// <![CDATA[
///     [RegisterComponent]
///     // Allow the system with utility functions for this component to modify it.
///     [Access(typeof(MySystem))]
///     public sealed class MyComponent : Component
///     {
///         public int Counter;
///     }
///
///     public sealed class MySystem : EntitySystem
///     {
///         public void AddToCounter(Entity<MyComponent> entity)
///         {
///             // Works, we're a friend of the other type.
///             entity.Comp.Counter += 1;
///         }
///     }
///
///     public sealed class OtherSystem : EntitySystem
///     {
///         public void AddToCounter(Entity<MyComponent> entity)
///         {
///             // Error RS2008: Tried to perform write access to member 'Counter' in type 'MyComponent', despite read access.
///             entity.Comp.Counter += 1;
///         }
///     }
/// ]]>
/// </code>
/// </example>
/// <seealso cref="AccessPermissions"/>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct
                | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method |
                AttributeTargets.Constructor)]
public sealed class AccessAttribute : Attribute
{
    /// <summary>Default bitmask applied to <see cref="Self"/>.</summary>
    public const AccessPermissions SelfDefaultPermissions = AccessPermissions.ReadWriteExecute;

    /// <summary>Default bitmask applied to <see cref="Friend"/>.</summary>
    public const AccessPermissions FriendDefaultPermissions = AccessPermissions.ReadWriteExecute;

    /// <summary>Default bitmask applied to <see cref="Other"/>.</summary>
    public const AccessPermissions OtherDefaultPermissions = AccessPermissions.Read;

    /// <summary>
    ///     Types that count as friends and receive <see cref="Friend"/> permissions instead of <see cref="Other"/>.
    /// </summary>
    /// <seealso cref="Friend"/>
    public readonly Type[] Friends;

    /// <summary>
    ///     Registers <paramref name="friends"/> as types that receive <see cref="Friend"/> permissions.
    /// </summary>
    /// <param name="friends">Types that receive <see cref="Friend"/> permissions.</param>
    public AccessAttribute(params Type[] friends)
    {
        Friends = friends;
    }

    /// <summary>
    ///     Access permissions for the declaring type or the type that owns the member.
    /// </summary>
    public AccessPermissions Self { get; set; } = SelfDefaultPermissions;

    /// <summary>
    ///     Access permissions for types specified as <see cref="Friends"/>.
    /// </summary>
    public AccessPermissions Friend { get; set; } = FriendDefaultPermissions;

    /// <summary>
    ///     Access permissions for types that aren't <see cref="Self"/> and aren't <see cref="Friend"/>.
    /// </summary>
    public AccessPermissions Other { get; set; } = OtherDefaultPermissions;
}
