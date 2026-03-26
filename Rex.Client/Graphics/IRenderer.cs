using Rex.Shared.Net.Messages;

namespace Rex.Client.Graphics;

/// <summary>Draws a frame given interpolated entity state (works for standalone and networked clients).</summary>
public interface IRenderer : IDisposable
{
    /// <summary>Called once after the window is created.</summary>
    void Initialize(IGameWindow window);

    /// <summary>Begins a new frame.</summary>
    void BeginFrame();

    /// <summary>Draw entities. <paramref name="alpha"/> matches the game loop blend factor for lerp.</summary>
    void RenderWorld(IReadOnlyList<EntityState> entities, float alpha);

    /// <summary>Finishes the frame.</summary>
    void EndFrame();
}
