using Silk.NET.Maths;

namespace Rex.Client.Graphics;

// TODO: This is a very basic interface and will likely need to be expanded and improved.

/// <summary>Window and GL context lifecycle. SDL or other backends implement this.</summary>
public interface IGameWindow : IDisposable
{
    /// <summary>Title shown in the window chrome.</summary>
    string Title { get; set; }

    int Width { get; }
    int Height { get; }
    bool IsOpen { get; }

    public event Action<Vector2D<int>>? OnResize;
    public event Action<Vector2D<int>>? OnFramebufferResize;
    public event Action? OnClosing;
    public event Action<bool>? OnFocusChanged;
    public event Action? OnLoad;
    public event Action<double>? OnUpdate;
    public event Action<double>? OnRender;

    /// <summary> Opens the window.</summary>
    void Open();

    /// <summary>Pumps platform events such as input, resize, and close.</summary>
    void PollEvents();

    /// <summary>Presents the rendered frame to the screen.</summary>
    void SwapBuffers();

    /// <summary>Closes and destroys the window.</summary>
    void Close();
}
