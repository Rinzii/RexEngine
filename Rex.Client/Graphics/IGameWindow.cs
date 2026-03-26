namespace Rex.Client.Graphics;

/// <summary>Window and GL context lifecycle. SDL or other backends implement this.</summary>
public interface IGameWindow : IDisposable
{
    /// <summary>Title shown in the window chrome.</summary>
    string Title { get; set; }

    int Width { get; }
    int Height { get; }
    bool IsOpen { get; }

    /// <summary>Creates and shows the window.</summary>
    void Open(string title, int width, int height);

    /// <summary>Pumps platform events such as input, resize, and close.</summary>
    void PollEvents();

    /// <summary>Presents the rendered frame to the screen.</summary>
    void SwapBuffers();

    /// <summary>Closes and destroys the window.</summary>
    void Close();
}