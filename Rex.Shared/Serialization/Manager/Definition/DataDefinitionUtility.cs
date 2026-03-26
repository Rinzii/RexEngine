using System;

namespace Rex.Shared.Serialization.Manager.Definition;

/// <summary>Helpers for YAML/data field tag strings when the full data-definition pipeline is wired up.</summary>
public static class DataDefinitionUtility
{
    /// <summary>Lowercases the first character (camelCase tag from PascalCase member name).</summary>
    public static string AutoGenerateTag(string name)
    {
        var span = name.AsSpan();
        return $"{char.ToLowerInvariant(span[0])}{span.Slice(1).ToString()}";
    }
}