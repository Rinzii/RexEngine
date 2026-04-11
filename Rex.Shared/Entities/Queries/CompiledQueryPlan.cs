using Rex.Shared.Entities.Storage;

namespace Rex.Shared.Entities.Queries;

internal sealed class CompiledQueryPlan
{
    public CompiledQueryPlan(QueryArchetypePlan[] archetypes)
    {
        Archetypes = archetypes;
    }

    public QueryArchetypePlan[] Archetypes { get; }
}

internal sealed class QueryArchetypePlan
{
    public QueryArchetypePlan(Archetype archetype, int[] accessColumnIndexes)
    {
        Archetype = archetype;
        AccessColumnIndexes = accessColumnIndexes;
    }

    public Archetype Archetype { get; }

    public int[] AccessColumnIndexes { get; }
}
