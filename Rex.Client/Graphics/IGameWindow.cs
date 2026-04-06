namespace Rex.Client.Graphics;

// TODO(IanP): Support multiple monitors, DPI and multiple windows.

/// <summary>Window surface and GL context lifecycle for a client process.</summary>
public interface IGameWindow : IDisposable
{
    /// <summary>Text shown in the platform window chrome.</summary>
    string Title { get; set; }

    /// <summary>Drawable width in pixels.</summary>
    int Width { get; }

    /// <summary>Drawable height in pixels.</summary>
    int Height { get; }

    /// <summary>True while the native window is open.</summary>
    bool IsOpen { get; }

    /// <summary>Opens the native window and blocks in the platform run loop when the backend needs it.</summary>
    /// <param name="title">Initial <see cref="Title"/>.</param>
    /// <param name="width">Initial client width in pixels.</param>
    /// <param name="height">Initial client height in pixels.</param>
    void Open(string title, int width, int height);

    /// <summary>Pumps input, resize and close notifications from the OS.</summary>
    void PollEvents();

    /// <summary>Presents the current back buffer.</summary>
    void SwapBuffers();

    /// <summary>Closes the window and releases native resources.</summary>
    void Close();
}
