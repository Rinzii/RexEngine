namespace Rex.Shared.Prototypes;

internal static class PrototypeValidation
{
    public static void ValidateIdentifier(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        foreach (char character in value)
        {
            if (char.IsAsciiLetter(character) || char.IsAsciiDigit(character) || character is '_' or '-')
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Value '{value}' must only contain ASCII letters, digits, underscores, or dashes.");
        }
    }

    public static void ValidateRelativePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

        if (path.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path '{path}' must use forward slashes.");
        }

        if (Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"Path '{path}' must be relative to the resources root.");
        }

        string[] segments = path.Split('/', StringSplitOptions.None);
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                throw new InvalidOperationException(
                    $"Path '{path}' contains an invalid segment '{segment}'.");
            }
        }
    }
}
