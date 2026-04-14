using System.Diagnostics.Contracts;

#if REX_ANALYZERS_IMPL
namespace Rex.Shared.Analyzers.Implementation;
#else
namespace Rex.Shared.Analyzers;
#endif

/// <summary>
///     A set of flags that dictate what kind of field and property access can occur for a given <see cref="AccessAttribute"/>.
/// </summary>
[Flags]
public enum AccessPermissions : byte
{
    /// <summary>No access is granted.</summary>
    None = 0,

    /// <summary>
    ///     Allows reading fields and properties through getters. Also allows calling methods marked with
    ///     <see cref="PureAttribute"/>.
    /// </summary>
    Read = 1 << 0,

    /// <summary>
    ///     Allows field and property write operations, for example using setters.
    /// </summary>
    Write = 1 << 1,

    /// <summary>
    ///     Allows executing methods.
    /// </summary>
    Execute = 1 << 2,

    /// <summary>Combines <see cref="Read"/> and <see cref="Write"/>.</summary>
    ReadWrite = Read | Write,

    /// <summary>Combines <see cref="Read"/> and <see cref="Execute"/>.</summary>
    ReadExecute = Read | Execute,

    /// <summary>Combines <see cref="Write"/> and <see cref="Execute"/>.</summary>
    WriteExecute = Write | Execute,

    /// <summary>Combines read write and execute access.</summary>
    ReadWriteExecute = Read | Write | Execute
}

/// <summary>Formats <see cref="AccessPermissions"/> as a Unix style rwx string.</summary>
public static class AccessPermissionsExtensions
{
    /// <summary>Maps flags to a three character permission string.</summary>
    /// <param name="permissions">Flags to render.</param>
    /// <returns>A string such as <c>rwx</c> or <c>r--</c>.</returns>
    public static string ToUnixPermissions(this AccessPermissions permissions)
    {
        return permissions switch
        {
            AccessPermissions.None => "---",
            AccessPermissions.Read => "r--",
            AccessPermissions.Write => "-w-",
            AccessPermissions.Execute => "--x",
            AccessPermissions.ReadWrite => "rw-",
            AccessPermissions.ReadExecute => "r-x",
            AccessPermissions.WriteExecute => "-wx",
            AccessPermissions.ReadWriteExecute => "rwx",
            _ => throw new ArgumentOutOfRangeException(nameof(permissions), permissions, null)
        };
    }
}
