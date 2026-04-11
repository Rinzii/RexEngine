namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Validation result tree for a serialized node graph.
/// </summary>
public sealed class ValidationNode
{
    /// <summary>
    /// Creates a validation result node.
    /// </summary>
    /// <param name="valid">Whether the node is valid.</param>
    /// <param name="message">Optional validation message.</param>
    public ValidationNode(bool valid, string? message = null)
    {
        Valid = valid;
        Message = message;
    }

    /// <summary>Gets a value indicating whether the node validated successfully.</summary>
    public bool Valid { get; }

    /// <summary>Gets the optional validation message.</summary>
    public string? Message { get; }

    /// <summary>Gets child validation results.</summary>
    public List<ValidationNode> Children { get; } = [];
}
