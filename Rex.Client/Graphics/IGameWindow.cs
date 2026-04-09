using Silk.NET.Maths;

namespace Rex.Client.Graphics;

// TODO: This is a very basic interface and will likely need to be expanded and improved.

/// <summary>Window and GL context lifecycle. SDL or other backends implement this.</summary>
public interface IGameWindow : IDisposable
{
    public string Title { get; set; }

    public Vector2D<int> Position { get; set; }
    public Vector2D<int> Size { get; set; }

    public bool Visible { get; set; }

    public bool IsMinimized { get; }
    public bool IsMaximized { get; }
    public bool IsFullscreen { get; }

    public event Action? OnReady;
    public event Action? OnClose;

    public event Action<bool>? OnFocusChanged;
    public event Action<Vector2D<int>>? OnMove;

    public event Action<Vector2D<int>>? OnResize;
    public event Action<Vector2D<int>>? OnFramebufferResize;

    public event Action<double>? OnUpdate;
    public event Action<double>? OnRender;

    // public event Action<WindowState>? StateChanged;
    // public event Action<string[]>? FileDrop;

    public void Open();
    public void Close();

    public void PollEvents();
}