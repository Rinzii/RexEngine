namespace Rex.Shared;

/// <summary>
/// Stable entity type names shared by simulation and network replication.
/// Use these instead of string literals where APIs are annotated with <c>ForbidLiteral</c>.
/// </summary>
public static class EntityTypeIds
{
    /// <summary>Default pawn type for prototype spawn/replicate paths.</summary>
    public const string Player = "player";
}
