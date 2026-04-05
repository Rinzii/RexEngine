namespace Rex.Client.Graphics;

/// <summary>
/// Engine level frame presentation binds to a window and exposes hooks to begin and end each frame without tying the renderer to a specific world model type.
/// </summary>
public interface IGraphicsRenderer : IDisposable
{
    /// <summary>
    /// Binds the renderer to a concrete window implementation.
    /// </summary>
    /// <param name="window">Surface that will receive GL or API setup from the renderer.</param>
    void Initialize(IGameWindow window);

    /// <summary>
    /// Begins a new presented frame.
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// Ends the frame and prepares for presentation.
    /// </summary>
    void EndFrame();
}
