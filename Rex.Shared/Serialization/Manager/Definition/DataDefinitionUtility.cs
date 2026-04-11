namespace Rex.Shared.Serialization.Manager.Definition;

/// <summary>Helpers for YAML and data field tag strings when the full data definition pipeline is wired up.</summary>
public static class DataDefinitionUtility
{
    /// <summary>Lowercases the first character (camelCase tag from PascalCase member name).</summary>
    public static string AutoGenerateTag(string name)
    {
        ReadOnlySpan<char> span = name.AsSpan();
        return char.ToLowerInvariant(span[0]) + span[1..].ToString();
    }
}
