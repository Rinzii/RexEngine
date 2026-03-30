using Rex.Client.Graphics;
using Rex.Shared.Analyzers;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Rex.Client;

public sealed class WindowCreator : IGameWindow
{
    private IWindow? WindowHandle { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsOpen { get; private set; }

    // TODO: Use these. They mirror what Silk.NET provides for Windowing.
    // TODO: Remove all of these disable stuff once we are actually using them.
#pragma warning disable CS0067 // Event is never used
    // ReSharper disable EventNeverSubscribedTo.Local
    // ReSharper disable EventNeverSubscribedTo.Global
    public event Action<Vector2D<int>>? OnResize;
    public event Action<Vector2D<int>>? OnFramebufferResize;
    public event Action? OnClosing;
    public event Action<bool>? OnFocusChanged;
    public event Action? OnLoad;
    public event Action<double>? OnUpdate;
    public event Action<double>? OnRender;
    // ReSharper restore EventNeverSubscribedTo.Global
    // ReSharper restore EventNeverSubscribedTo.Local
#pragma warning restore CS0067 // Event is never used

    public void Dispose()
    {
        WindowHandle?.Dispose();
    }
    public void Open(string title, int width, int height)
    {
        Title = title;
        Width = width;
        Height = height;

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(Width, Height),
            // ReSharper disable once ArrangeThisQualifier
            Title = this.Title
        };

        var window = Window.Create(options);
        WindowHandle = window;
        WindowHandle.Load += HandleLoad;
        WindowHandle.Update += HandleUpdate;
        WindowHandle.Render += HandleRender;
        window.Run();
        IsOpen = true;
    }
    public void Close()
    {
        WindowHandle?.Close();
        IsOpen = false;
    }

    public void PollEvents()
    {
        throw new NotImplementedException();
    }

    public void SwapBuffers()
    {
        throw new NotImplementedException();
    }

    private void HandleLoad()
    {
        var window = WindowHandle ?? throw new InvalidOperationException("Load fired before the window was assigned.");
        var input = window.CreateInput();
        foreach (var t in input.Keyboards)
        {
            t.KeyDown += KeyDown;
        }
    }

    private void HandleUpdate(double deltaTime)
    {

    }

    private void HandleRender(double deltaTime)
    {

    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (key == Key.Escape)
        {
            Close();
        }
    }
}



