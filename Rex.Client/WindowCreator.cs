using Rex.Client.Graphics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Rex.Client;

/// <summary>Silk.NET window registered in DI as <see cref="IGameWindow"/>.</summary>
public sealed class WindowCreator : IGameWindow
{
    private IWindow? WindowHandle { get; set; }

    /// <inheritdoc />
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc />
    public int Width { get; private set; }

    /// <inheritdoc />
    public int Height { get; private set; }

    /// <inheritdoc />
    public bool IsOpen { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        WindowHandle?.Dispose();
    }

    /// <inheritdoc />
    public void Open(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;

        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(Width, Height),
            // ReSharper disable once ArrangeThisQualifier
            Title = Title
        };

        IWindow window = Window.Create(options);
        WindowHandle = window;
        WindowHandle.Load += HandleLoad;
        WindowHandle.Update += HandleUpdate;
        WindowHandle.Render += HandleRender;
        window.Run();
        IsOpen = true;
    }

    /// <inheritdoc />
    public void Close()
    {
        WindowHandle?.Close();
        IsOpen = false;
    }

    /// <inheritdoc />
    public void PollEvents()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public void SwapBuffers()
    {
        throw new NotImplementedException();
    }

    private void HandleLoad()
    {
        IWindow window = WindowHandle
                         ?? throw new InvalidOperationException("Load fired before the window was assigned.");
        IInputContext input = window.CreateInput();
        foreach (IKeyboard t in input.Keyboards)
        {
            t.KeyDown += KeyDown;
        }
    }

    private void HandleUpdate(double deltaTime) { }

    private void HandleRender(double deltaTime) { }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (key == Key.Escape)
        {
            Close();
        }
    }

    // TODO(IanP): Wire Silk resize and input events once the client loop consumes them.
#pragma warning disable CS0067
    // ReSharper disable EventNeverSubscribedTo.Local
    // ReSharper disable EventNeverSubscribedTo.Global
    /// <summary>Forwarded host resize in pixels.</summary>
    public event Action<Vector2D<int>>? OnResize;

    /// <summary>Forwarded framebuffer resize in pixels.</summary>
    public event Action<Vector2D<int>>? OnFramebufferResize;

    /// <summary>Raised when the host window is closing.</summary>
    public event Action? OnClosing;

    /// <summary>Raised when keyboard focus changes.</summary>
    public event Action<bool>? OnFocusChanged;

    /// <summary>Raised once after the native window loads.</summary>
    public event Action? OnLoad;

    /// <summary>Raised each host update tick with delta seconds.</summary>
    public event Action<double>? OnUpdate;

    /// <summary>Raised each host render tick with delta seconds.</summary>
    public event Action<double>? OnRender;
    // ReSharper restore EventNeverSubscribedTo.Global
    // ReSharper restore EventNeverSubscribedTo.Local
#pragma warning restore CS0067
}
