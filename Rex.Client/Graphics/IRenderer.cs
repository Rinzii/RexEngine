using Rex.Shared.Net.Messages;

namespace Rex.Client.Graphics;

/// <summary>Draws a frame from interpolated entity state for standalone or networked clients.</summary>
public interface IRenderer : IDisposable
{
    /// <summary>Called once after the window is created.</summary>
    void Initialize(IGameWindow window);

    /// <summary>Begins a new frame.</summary>
    void BeginFrame();

    /// <summary>Draw entities for one frame. <paramref name="alpha"/> is the game loop interpolation factor.</summary>
    void RenderWorld(IReadOnlyList<EntityState> entities, float alpha);

    /// <summary>Finishes the frame.</summary>
    void EndFrame();
}