using Rex.Shared.Components;
using Rex.Shared.Components.Registration;

namespace Rex.Shared.Entities.Storage;

internal sealed class Archetype
{
    private const int DefaultChunkCapacity = 64;
    private readonly int[] _componentIds;
    private readonly List<ArchetypeChunk> _chunks = [];

    public Archetype(ArchetypeSignature signature, ComponentRegistry registry)
    {
        Signature = signature;
        _componentIds = signature.ToArray();
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ArchetypeSignature Signature { get; }

    public ComponentRegistry Registry { get; }

    public int Count { get; private set; }

    public ReadOnlySpan<int> ComponentIds => _componentIds;

    public int ChunkCount => _chunks.Count;

    public ArchetypeRowLocation AddEntity(EntityId entity)
    {
        ArchetypeChunk chunk = GetWritableChunk(out int chunkIndex);
        int rowIndex = chunk.AddEntity(entity);
        Count++;
        return new ArchetypeRowLocation(chunkIndex, rowIndex);
    }

    public EntityId GetEntity(in ArchetypeRowLocation location)
    {
        return _chunks[location.ChunkIndex].GetEntity(location.RowIndex);
    }

    public ArchetypeChunk GetChunk(int chunkIndex)
    {
        return _chunks[chunkIndex];
    }

    public ComponentColumn<T> GetColumn<T>(int componentId, int chunkIndex)
        where T : struct, IComponent
    {
        return _chunks[chunkIndex].GetColumn<T>(GetColumnIndex(componentId));
    }

    public IComponentColumn GetColumn(int componentId, int chunkIndex)
    {
        return _chunks[chunkIndex].GetColumn(GetColumnIndex(componentId));
    }

    public int GetColumnIndex(int componentId)
    {
        int index = Signature.IndexOf(componentId);
        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Component id {componentId} is not part of the current archetype signature.");
        }

        return index;
    }

    public void RemoveEntity(in ArchetypeRowLocation location, out bool moved, out EntityId movedEntity,
        out ArchetypeRowLocation movedLocation)
    {
        if (!location.IsValid || location.ChunkIndex >= _chunks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(location), location, "Location must be inside the archetype.");
        }

        ArchetypeChunk targetChunk = _chunks[location.ChunkIndex];
        if (location.RowIndex < 0 || location.RowIndex >= targetChunk.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(location), location, "Location must be inside the archetype.");
        }

        int donorChunkIndex = _chunks.Count - 1;
        ArchetypeChunk donorChunk = _chunks[donorChunkIndex];
        int donorRowIndex = donorChunk.Count - 1;
        moved = location.ChunkIndex != donorChunkIndex || location.RowIndex != donorRowIndex;
        movedEntity = moved ? donorChunk.GetEntity(donorRowIndex) : EntityId.Invalid;
        movedLocation = moved ? location : ArchetypeRowLocation.Invalid;

        if (moved)
        {
            targetChunk.SetEntity(location.RowIndex, movedEntity);
            for (int i = 0; i < _componentIds.Length; i++)
            {
                donorChunk.GetColumn(i).CopyValueTo(donorRowIndex, targetChunk.GetColumn(i), location.RowIndex);
            }
        }

        donorChunk.RemoveLastRow();
        if (donorChunk.Count == 0)
        {
            _chunks.RemoveAt(donorChunkIndex);
        }

        Count--;
    }

    private ArchetypeChunk GetWritableChunk(out int chunkIndex)
    {
        if (_chunks.Count == 0 || !_chunks[^1].HasCapacity)
        {
            _chunks.Add(CreateChunk());
        }

        chunkIndex = _chunks.Count - 1;
        return _chunks[chunkIndex];
    }

    private ArchetypeChunk CreateChunk()
    {
        var columns = new IComponentColumn[_componentIds.Length];
        for (int i = 0; i < _componentIds.Length; i++)
        {
            columns[i] = Registry.GetRegistration(_componentIds[i]).CreateColumn();
        }

        return new ArchetypeChunk(columns, DefaultChunkCapacity);
    }
}
