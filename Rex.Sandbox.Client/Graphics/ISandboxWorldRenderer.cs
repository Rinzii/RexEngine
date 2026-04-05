using Rex.Client.Graphics;
using Rex.Sandbox.Shared.Net.Messages;

namespace Rex.Sandbox.Client.Graphics;

/// <summary>
/// Draws sandbox <see cref="EntityState"/> snapshots for a frame. Sample code only. Implements engine <see cref="IGameRenderer{TEntity}"/>.
/// </summary>
public interface ISandboxWorldRenderer : IGameRenderer<EntityState>;
