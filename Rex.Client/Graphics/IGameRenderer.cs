namespace Rex.Client.Graphics;

/// <summary>
/// Renders an interpolated snapshot of entities defined by the game for one frame.
/// </summary>
/// <typeparam name="TEntity">View or simulation type owned by the game assembly (not the engine).</typeparam>
public interface IGameRenderer<in TEntity> : IGraphicsRenderer
{
    /// <summary>
    /// Draws the world snapshot for the current frame.
    /// </summary>
    /// <param name="entities">Latest authoritative or predicted entities to visualize.</param>
    /// <param name="interpolationAlpha">Blend factor between simulation ticks from <see cref="Rex.Shared.Timing.TickClock"/>.</param>
    void RenderWorld(IReadOnlyList<TEntity> entities, float interpolationAlpha);
}
