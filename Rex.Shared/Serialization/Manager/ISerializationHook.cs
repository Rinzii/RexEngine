namespace Rex.Shared.Serialization.Manager;

/// <summary>
/// Optional hook points invoked around reflected serialization operations.
/// </summary>
public interface ISerializationHook
{
    /// <summary>
    /// Called before a type is written.
    /// </summary>
    void BeforeSerialization();

    /// <summary>
    /// Called after a type is read.
    /// </summary>
    void AfterDeserialization();
}
