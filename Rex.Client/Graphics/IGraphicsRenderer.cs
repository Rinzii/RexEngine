namespace Rex.Client.Graphics;

/// <summary>
/// Engine-level frame presentation: window binding and begin/end frame hooks without a specific world model type.
/// </summary>
public interface IGraphicsRenderer : IDisposable
{
    void Initialize(IGameWindow window);

    void BeginFrame();

    void EndFrame();
}
