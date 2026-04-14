namespace Rex.Shared.IoC;

/// <summary>
/// Called after the IoC manager injects all <see cref="DependencyAttribute"/> fields.
/// </summary>
public interface IPostInjectInit
{
    /// <summary>
    /// Called after field injection completes for this instance.
    /// </summary>
    void PostInject();
}
