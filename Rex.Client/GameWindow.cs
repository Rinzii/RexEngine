using Rex.Client.Graphics;
using Silk.NET.Windowing;
using Silk.NET.Maths;

namespace Rex.Client;

public sealed class GameWindow : IGameWindow
{
    private readonly IWindow? _windowHandle;

    public string Title
    {
        get => _windowHandle?.Title ?? "<unknown>";
        set => _windowHandle?.Title = value;
    }

    public Vector2D<int> Position
    {
        get => _windowHandle?.Position ?? new Vector2D<int>(0, 0);
        set => _windowHandle?.Position = value;
    }

    public Vector2D<int> Size
    {
        get => _windowHandle?.Size ?? new Vector2D<int>(0, 0);
        set => _windowHandle?.Size = value;
    }

    public bool Visible
    {
        get => _windowHandle?.IsVisible ?? false;
        set => _windowHandle?.IsVisible = value;
    }

    public bool IsMinimized => _windowHandle?.WindowState == WindowState.Minimized;
    public bool IsMaximized => _windowHandle?.WindowState == WindowState.Maximized;
    public bool IsFullscreen => _windowHandle?.WindowState == WindowState.Fullscreen;

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

    public GameWindow(string title, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(width, height),
            Title = title
        };

        _windowHandle = Window.Create(options);

        _windowHandle.Load += HandleLoadEvent;
        _windowHandle.Closing += HandleClosingEvent;

        _windowHandle.FocusChanged += (focused) => OnFocusChanged?.Invoke(focused);
        _windowHandle.Move += (pos) => OnMove?.Invoke(pos);

        _windowHandle.Resize += (size) => OnResize?.Invoke(size);
        _windowHandle.FramebufferResize += (size) => OnFramebufferResize?.Invoke(size);

        _windowHandle.Update += HandleUpdateEvent;
        _windowHandle.Render += HandleRenderEvent;
    }

    public void Dispose()
    {
        _windowHandle?.Dispose();
    }

    private void HandleLoadEvent()
    {
        OnReady?.Invoke();
    }

    private void HandleClosingEvent()
    {
        OnClose?.Invoke();
    }

    private void HandleUpdateEvent(double obj)
    {
        OnUpdate?.Invoke(obj);
    }

    private void HandleRenderEvent(double obj)
    {
        OnRender?.Invoke(obj);
    }

    public void Open()
    {
        _windowHandle?.Run();
    }

    public void Close()
    {
        _windowHandle?.Close();
    }

    public void PollEvents()
    {
        _windowHandle?.DoEvents();
    }
}