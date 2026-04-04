using Rex.Sandbox.Shared.Net.Messages;

namespace Rex.Sandbox.Client.Graphics;

/// <summary>
/// Draws Sandbox entity state for a frame.
/// </summary>
public interface IRenderer : IDisposable
{
    void Initialize(Rex.Client.Graphics.IGameWindow window);
    void BeginFrame();
    void RenderWorld(IReadOnlyList<EntityState> entities, float alpha);
    void EndFrame();
}
