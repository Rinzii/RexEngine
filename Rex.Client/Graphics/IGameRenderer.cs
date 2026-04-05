namespace Rex.Client.Graphics;

/// <summary>
/// Renders an interpolated snapshot of game-defined entities for one frame.
/// </summary>
/// <typeparam name="TEntity">View or simulation type owned by the game assembly (not the engine).</typeparam>
public interface IGameRenderer<TEntity> : IGraphicsRenderer
{
    void RenderWorld(IReadOnlyList<TEntity> entities, float interpolationAlpha);
}
