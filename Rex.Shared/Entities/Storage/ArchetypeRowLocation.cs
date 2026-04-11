namespace Rex.Shared.Entities.Storage;

internal readonly record struct ArchetypeRowLocation(int ChunkIndex, int RowIndex)
{
    public static ArchetypeRowLocation Invalid { get; } = new(-1, -1);

    public bool IsValid => ChunkIndex >= 0 && RowIndex >= 0;
}
