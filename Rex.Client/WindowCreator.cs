using Rex.Client.Graphics;
using Rex.Shared.Analyzers;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Rex.Client;

public sealed class WindowCreator : IGameWindow
{
    private readonly IWindow? _window;
    public string Title { get; set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsOpen { get; private set; }

    public static Action? OnLoad;
    public static Action<double>? OnUpdate;
    public static Action<double>? OnRender;

    public WindowCreator(string title, int width, int height)
    {

        OnLoad += OnLoad1;
        OnRender += OnRender1;
        OnUpdate += OnUpdate1;

        Title = title;
        Width = width;
        Height = height;

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(Width, Height),
            Title
            // ReSharper disable once ArrangeThisQualifier
            = this.Title
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
    }

    public void Dispose()
    {
        _window?.Dispose();
    }
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        _window?.Run();
        IsOpen = true;
    }
    public void Close()
    {

        _window?.Close();
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

    private void OnLoad1()
    {
        IInputContext? input = _window?.CreateInput();
        for (int i = 0; i < input?.Keyboards.Count; i++)
        {
            input.Keyboards[i].KeyDown += KeyDown;
        }
    }

    private void OnUpdate1(double deltaTime)
    {

    }

    private void OnRender1(double deltaTime)
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



